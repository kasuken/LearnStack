using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using PlanTier = LearnStack.Data.Models.PlanTier;

namespace LearnStack.Billing;

/// <summary>
/// Live Stripe implementation of <see cref="IBillingProvider"/>, selected by
/// <c>Billing:Provider = Stripe</c>. Upgrades go through hosted Stripe Checkout, self-service
/// changes through the hosted Customer Portal, and plan-state changes are applied only from
/// signature-verified webhooks.
/// </summary>
/// <remarks>
/// <para>
/// The LearnStack user id travels to Stripe in three places, because each read path sees a
/// different object: <c>client_reference_id</c> and <c>metadata</c> on the Checkout Session,
/// and <c>metadata</c> on the Subscription it creates. Subscription events therefore identify
/// their LearnStack user without an extra API call; when metadata is missing (for example a
/// subscription created by hand in the Stripe Dashboard) the customer id is matched against
/// <see cref="UserPlan.BillingProviderCustomerId"/> instead.
/// </para>
/// <para>
/// Tier is resolved from the subscription's price id against the configured Pro prices, so a
/// price that exists in Stripe but is not configured here resolves to no paid tier rather
/// than silently granting Pro.
/// </para>
/// </remarks>
public sealed class StripeBillingProvider : IBillingProvider
{
    internal const string UserIdMetadataKey = "learnstack_user_id";

    /// <summary>Query key the checkout success URL carries the Stripe Checkout Session id in.</summary>
    public const string CheckoutSessionIdQueryKey = "session_id";

    /// <summary>Query key and value the checkout cancel URL carries, so the return page can say checkout was abandoned.</summary>
    public const string CheckoutOutcomeQueryKey = "checkout";

    /// <inheritdoc cref="CheckoutOutcomeQueryKey"/>
    public const string CheckoutCancelledValue = "cancelled";

    private const string CheckoutSessionIdPlaceholder = "{CHECKOUT_SESSION_ID}";
    private const string AlreadySubscribedReason = "You already have a Pro subscription. Use Manage billing to change it.";
    private const string CheckoutFailedReason = "We couldn't start checkout just now. Please try again in a moment.";

    private static readonly string[] EntitlingStatuses = ["active", "trialing", "past_due"];

    // Statuses in which Stripe will not bill the subscription again on its own. "incomplete" is
    // deliberately absent: it is a first payment still in progress, which either becomes active
    // or expires into "incomplete_expired".
    private static readonly string[] EndedStatuses = ["canceled", "unpaid", "incomplete_expired", "paused"];

    private static readonly TimeSpan DefaultPaymentFailureGracePeriod = TimeSpan.FromDays(7);

    // Added to Stripe's next retry time so the grace period outlasts the retry's own webhook
    // delivery; without it paid access would lapse in the seconds between a retry succeeding
    // and its invoice.paid event arriving.
    private static readonly TimeSpan RetryWebhookAllowance = TimeSpan.FromDays(1);

    private readonly BillingOptions _options;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<StripeBillingProvider> _logger;
    private readonly IStripeClient _stripeClient;

    public StripeBillingProvider(
        IOptions<BillingOptions> options,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<StripeBillingProvider> logger,
        IStripeClient? stripeClient = null)
    {
        _options = options.Value;
        _contextFactory = contextFactory;
        _logger = logger;
        _stripeClient = stripeClient ?? new StripeClient(_options.ApiKey);
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string userId,
        PlanTier targetTier,
        BillingInterval interval = BillingInterval.Yearly,
        string? accountEmail = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (targetTier != PlanTier.Pro)
            return new CheckoutSessionResult(false, null, "Only upgrading to Pro requires checkout.");

        var priceId = ResolvePriceId(targetTier, interval);

        var stored = await GetStoredBillingAsync(userId, cancellationToken).ConfigureAwait(false);
        if (stored?.Tier == PlanTier.Pro)
            return new CheckoutSessionResult(false, null, AlreadySubscribedReason);

        var existingCustomerId = stored?.CustomerId;
        if (!string.IsNullOrWhiteSpace(existingCustomerId))
        {
            // The stored tier lags Stripe until the webhook lands, so a second tab or a quick
            // double submit would otherwise open a second checkout and a second subscription.
            try
            {
                if (await HasLiveSubscriptionAsync(existingCustomerId, cancellationToken).ConfigureAwait(false))
                    return new CheckoutSessionResult(false, null, AlreadySubscribedReason);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Could not check existing Stripe subscriptions for user {UserId}.", userId);
                return new CheckoutSessionResult(false, null, CheckoutFailedReason);
            }
        }

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = existingCustomerId,
            // Stripe rejects customer and customer_email together, and an existing customer
            // already carries its own address, so this only seeds the first checkout. Left
            // unset, Checkout collects the email itself and prefills it from the visitor's
            // Stripe Link account, which is how a customer ends up under an address that has
            // nothing to do with the signed-in LearnStack account.
            CustomerEmail = string.IsNullOrWhiteSpace(existingCustomerId) && !string.IsNullOrWhiteSpace(accountEmail)
                ? accountEmail
                : null,
            ClientReferenceId = userId,
            Metadata = new Dictionary<string, string> { [UserIdMetadataKey] = userId },
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            AllowPromotionCodes = true,
            // Stripe substitutes {CHECKOUT_SESSION_ID} itself, which lets the return page confirm
            // the purchase straight away instead of waiting for the webhook.
            SuccessUrl = _options.CheckoutSuccessUrl!.Contains(CheckoutSessionIdPlaceholder, StringComparison.Ordinal)
                ? _options.CheckoutSuccessUrl
                : AppendQuery(_options.CheckoutSuccessUrl, $"{CheckoutSessionIdQueryKey}={CheckoutSessionIdPlaceholder}"),
            CancelUrl = _options.CheckoutCancelUrl!.Contains($"{CheckoutOutcomeQueryKey}=", StringComparison.Ordinal)
                ? _options.CheckoutCancelUrl
                : AppendQuery(_options.CheckoutCancelUrl, $"{CheckoutOutcomeQueryKey}={CheckoutCancelledValue}"),
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { [UserIdMetadataKey] = userId },
            },
        };

        try
        {
            var session = await new SessionService(_stripeClient)
                .CreateAsync(sessionOptions, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(session.Url)
                ? new CheckoutSessionResult(false, null, "Stripe did not return a checkout URL.")
                : new CheckoutSessionResult(true, session.Url, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout session creation failed for user {UserId} on price {PriceId}.", userId, priceId);
            return new CheckoutSessionResult(false, null, CheckoutFailedReason);
        }
    }

    public async Task<ParsedBillingWebhookEvent?> GetCompletedCheckoutAsync(
        string userId,
        string checkoutSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutSessionId);

        Session session;
        try
        {
            var options = new SessionGetOptions();
            options.AddExpand("subscription");
            session = await new SessionService(_stripeClient)
                .GetAsync(checkoutSessionId, options, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Could not read Stripe checkout session {SessionId} for user {UserId}.", checkoutSessionId, userId);
            return null;
        }

        if (session.Mode != "subscription" || session.Status != "complete" || session.Subscription is not { } subscription)
            return null;

        // The session id arrives in a URL anyone can edit, so it only counts for the user it was created for.
        var sessionUserId = session.ClientReferenceId ?? GetMetadataUserId(session.Metadata);
        if (sessionUserId != userId)
        {
            _logger.LogWarning("Stripe checkout session {SessionId} does not belong to user {UserId}; ignoring.", checkoutSessionId, userId);
            return null;
        }

        var parsed = MapSubscription(session.Id, "checkout.session.returned", userId, subscription, deleted: false, occurredAtUtc: null);
        return parsed with { BillingProviderCustomerId = session.CustomerId ?? parsed.BillingProviderCustomerId };
    }

    public async Task<bool> UpdateCustomerEmailAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var customerId = await GetStoredCustomerIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(customerId))
            return true;

        try
        {
            await new CustomerService(_stripeClient)
                .UpdateAsync(customerId, new CustomerUpdateOptions { Email = email }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Could not update the Stripe customer email for user {UserId} (customer {CustomerId}).", userId, customerId);
            return false;
        }
    }

    public async Task<PortalSessionResult> CreatePortalSessionAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var customerId = await GetStoredCustomerIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(customerId))
            return new PortalSessionResult(false, null, "You don't have a billing account yet. Upgrade to Pro first.");

        try
        {
            var session = await new Stripe.BillingPortal.SessionService(_stripeClient)
                .CreateAsync(
                    new Stripe.BillingPortal.SessionCreateOptions
                    {
                        Customer = customerId,
                        ReturnUrl = _options.PortalReturnUrl,
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(session.Url)
                ? new PortalSessionResult(false, null, "Stripe did not return a portal URL.")
                : new PortalSessionResult(true, session.Url, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe billing portal session creation failed for user {UserId}.", userId);
            return new PortalSessionResult(false, null, "We couldn't open the billing portal just now. Please try again in a moment.");
        }
    }

    public async Task<SubscriptionCancellationResult> CancelSubscriptionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var customerId = await GetStoredCustomerIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(customerId))
            return SubscriptionCancellationResult.Success;

        var subscriptionService = new SubscriptionService(_stripeClient);

        try
        {
            // Every subscription on the customer, not only the stored one: a duplicate checkout
            // leaves a second subscription LearnStack never recorded, and it would keep charging.
            var subscriptions = await subscriptionService
                .ListAsync(new SubscriptionListOptions { Customer = customerId, Limit = 100 }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (var subscription in subscriptions.Data.Where(sub => sub.Status is not ("canceled" or "incomplete_expired")))
            {
                try
                {
                    await subscriptionService
                        .CancelAsync(subscription.Id, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
                {
                    // Already gone on Stripe's side, which is the outcome we wanted.
                }
            }

            return SubscriptionCancellationResult.Success;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe subscription cancellation failed for user {UserId} (customer {CustomerId}).", userId, customerId);
            return new SubscriptionCancellationResult(false, "Stripe subscription cancellation failed.");
        }
    }

    public Task<WebhookVerificationResult> VerifyWebhookSignatureAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(_options.WebhookSigningSecret))
            return Task.FromResult(new WebhookVerificationResult(false, "Webhook signing secret is not configured."));

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return Task.FromResult(new WebhookVerificationResult(false, "Missing webhook signature header."));

        try
        {
            EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _options.WebhookSigningSecret,
                throwOnApiVersionMismatch: false);

            return Task.FromResult(new WebhookVerificationResult(true, null));
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Rejected a billing webhook delivery with an invalid Stripe signature.");
            return Task.FromResult(new WebhookVerificationResult(false, "Stripe signature verification failed."));
        }
    }

    public async Task<ParsedBillingWebhookEvent?> ParseWebhookEventAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ParseEvent(payload, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Could not parse a signature-verified Stripe webhook payload.");
            return null;
        }

        return stripeEvent.Type switch
        {
            EventTypes.CheckoutSessionCompleted => ParseCheckoutCompleted(stripeEvent),
            EventTypes.CustomerSubscriptionCreated or EventTypes.CustomerSubscriptionUpdated or EventTypes.CustomerSubscriptionDeleted
                => await ParseSubscriptionEventAsync(stripeEvent, cancellationToken).ConfigureAwait(false),
            EventTypes.InvoicePaymentFailed => await MapInvoicePaymentFailedAsync(stripeEvent, cancellationToken).ConfigureAwait(false),
            EventTypes.InvoicePaid => await MapInvoicePaidAsync(stripeEvent, cancellationToken).ConfigureAwait(false),
            _ => null,
        };
    }

    private ParsedBillingWebhookEvent? ParseCheckoutCompleted(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Session session || session.Mode != "subscription")
            return null;

        var userId = session.ClientReferenceId ?? GetMetadataUserId(session.Metadata);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Stripe checkout session {SessionId} completed without a LearnStack user id; ignoring.", session.Id);
            return null;
        }

        return new ParsedBillingWebhookEvent(
            ProviderEventId: stripeEvent.Id,
            EventType: stripeEvent.Type,
            TargetUserId: userId,
            NewTier: null,
            PeriodEndsAtUtc: null,
            BillingProviderCustomerId: session.CustomerId,
            BillingProviderSubscriptionId: session.SubscriptionId,
            OccurredAtUtc: GetOccurredAtUtc(stripeEvent));
    }

    private async Task<ParsedBillingWebhookEvent?> ParseSubscriptionEventAsync(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
            return null;

        var userId = GetMetadataUserId(subscription.Metadata)
            ?? await ResolveUserIdFromCustomerIdAsync(subscription.CustomerId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning(
                "Stripe subscription {SubscriptionId} for customer {CustomerId} maps to no LearnStack user; ignoring.",
                subscription.Id,
                subscription.CustomerId);
            return null;
        }

        return MapSubscription(
            stripeEvent.Id,
            stripeEvent.Type,
            userId,
            subscription,
            deleted: stripeEvent.Type == EventTypes.CustomerSubscriptionDeleted,
            GetOccurredAtUtc(stripeEvent));
    }

    /// <summary>
    /// Maps a subscription's state onto the plan it entitles <paramref name="userId"/> to. Shared
    /// by subscription webhooks and the checkout-return confirmation, so both apply identical rules.
    /// </summary>
    private ParsedBillingWebhookEvent MapSubscription(
        string eventId,
        string eventType,
        string userId,
        Subscription subscription,
        bool deleted,
        DateTime? occurredAtUtc)
    {
        var linkOnly = new ParsedBillingWebhookEvent(
            ProviderEventId: eventId,
            EventType: eventType,
            TargetUserId: userId,
            NewTier: null,
            PeriodEndsAtUtc: null,
            BillingProviderCustomerId: subscription.CustomerId,
            BillingProviderSubscriptionId: subscription.Id,
            OccurredAtUtc: occurredAtUtc);

        if (deleted || EndedStatuses.Contains(subscription.Status))
            return linkOnly with { NewTier = PlanTier.Starter, ClearsGracePeriod = true };

        if (!EntitlingStatuses.Contains(subscription.Status))
            return linkOnly;

        var periodEndsAtUtc = GetCurrentPeriodEndUtc(subscription);
        return linkOnly with
        {
            NewTier = ResolveTier(subscription),
            PeriodEndsAtUtc = periodEndsAtUtc,
            ClearsGracePeriod = subscription.Status is "active" or "trialing",
            HasCancellationSchedule = true,
            // The portal schedules a cancellation either as cancel_at_period_end or, on newer
            // API versions, as an explicit cancel_at; both mean "stop renewing on this date".
            CancelsAtUtc = subscription.CancelAt?.ToUniversalTime() ?? (subscription.CancelAtPeriodEnd ? periodEndsAtUtc : null),
        };
    }

    private async Task<ParsedBillingWebhookEvent?> MapInvoicePaymentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
            return null;

        var userId = await ResolveUserIdFromCustomerIdAsync(invoice.CustomerId, cancellationToken).ConfigureAwait(false);
        if (userId is null)
            return null;

        var gracePeriodEndsAtUtc = invoice.NextPaymentAttempt is { } nextPaymentAttempt
            ? nextPaymentAttempt.ToUniversalTime() + RetryWebhookAllowance
            : DateTime.UtcNow + DefaultPaymentFailureGracePeriod;

        return new ParsedBillingWebhookEvent(
            ProviderEventId: stripeEvent.Id,
            EventType: stripeEvent.Type,
            TargetUserId: userId,
            NewTier: null,
            PeriodEndsAtUtc: null,
            GracePeriodEndsAtUtc: gracePeriodEndsAtUtc,
            OccurredAtUtc: GetOccurredAtUtc(stripeEvent));
    }

    private async Task<ParsedBillingWebhookEvent?> MapInvoicePaidAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
            return null;

        var userId = await ResolveUserIdFromCustomerIdAsync(invoice.CustomerId, cancellationToken).ConfigureAwait(false);
        if (userId is null)
            return null;

        // Only the grace period is cleared here. Tier and renewal date come from subscription
        // events alone: an invoice can belong to a price LearnStack does not sell, or arrive
        // after the subscription was deleted, and its period_end is the period just billed
        // rather than the next renewal.
        return new ParsedBillingWebhookEvent(
            ProviderEventId: stripeEvent.Id,
            EventType: stripeEvent.Type,
            TargetUserId: userId,
            NewTier: null,
            PeriodEndsAtUtc: null,
            ClearsGracePeriod: true,
            OccurredAtUtc: GetOccurredAtUtc(stripeEvent));
    }

    private PlanTier ResolveTier(Subscription subscription)
    {
        var priceIds = subscription.Items?.Data?
            .Select(item => item.Price?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList() ?? [];

        if (priceIds.Any(id => id == _options.ProMonthlyPriceId || id == _options.ProYearlyPriceId))
            return PlanTier.Pro;

        _logger.LogWarning(
            "Stripe subscription {SubscriptionId} carries no configured LearnStack price ({PriceIds}); treating as Starter.",
            subscription.Id,
            string.Join(", ", priceIds));
        return PlanTier.Starter;
    }

    private static DateTime? GetCurrentPeriodEndUtc(Subscription subscription)
    {
        var periodEnds = subscription.Items?.Data?
            .Select(item => item.CurrentPeriodEnd)
            .Where(end => end != default)
            .ToList();

        return periodEnds is { Count: > 0 } ? periodEnds.Max().ToUniversalTime() : null;
    }

    private static DateTime? GetOccurredAtUtc(Event stripeEvent) =>
        stripeEvent.Created == default ? null : stripeEvent.Created.ToUniversalTime();

    private string ResolvePriceId(PlanTier tier, BillingInterval interval) => (tier, interval) switch
    {
        (PlanTier.Pro, BillingInterval.Monthly) => _options.ProMonthlyPriceId!,
        (PlanTier.Pro, BillingInterval.Yearly) => _options.ProYearlyPriceId!,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "No Stripe price is configured for this plan tier."),
    };

    private static string? GetMetadataUserId(IDictionary<string, string>? metadata) =>
        metadata is not null && metadata.TryGetValue(UserIdMetadataKey, out var userId) && !string.IsNullOrWhiteSpace(userId)
            ? userId
            : null;

    private async Task<string?> ResolveUserIdFromCustomerIdAsync(string? customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.UserPlans.AsNoTracking()
            .Where(plan => plan.BillingProviderCustomerId == customerId)
            .Select(plan => plan.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> HasLiveSubscriptionAsync(string customerId, CancellationToken cancellationToken)
    {
        var subscriptions = await new SubscriptionService(_stripeClient)
            .ListAsync(new SubscriptionListOptions { Customer = customerId, Limit = 100 }, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return subscriptions.Data.Any(subscription => EntitlingStatuses.Contains(subscription.Status));
    }

    private static string AppendQuery(string url, string query) =>
        url + (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + query;

    private sealed record StoredBilling(PlanTier Tier, string? CustomerId);

    private async Task<StoredBilling?> GetStoredBillingAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.UserPlans.AsNoTracking()
            .Where(plan => plan.UserId == userId)
            .Select(plan => new StoredBilling(plan.Tier, plan.BillingProviderCustomerId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string?> GetStoredCustomerIdAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.UserPlans.AsNoTracking()
            .Where(plan => plan.UserId == userId)
            .Select(plan => plan.BillingProviderCustomerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
