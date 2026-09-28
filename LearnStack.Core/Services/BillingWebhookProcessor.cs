using LearnStack.Billing;
using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

/// <summary>
/// Verifies and idempotently applies billing-provider webhook deliveries. Under
/// <c>NullBillingProvider</c>, <see cref="IBillingProvider.VerifyWebhookSignatureAsync"/>
/// always fails, so this never applies state without a real provider configured - there is no
/// way for an unauthenticated request to change anyone's plan.
/// </summary>
public class BillingWebhookProcessor(
    IBillingProvider billingProvider,
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEntitlementService entitlements) : IBillingWebhookProcessor
{
    public async Task<BillingWebhookProcessingResult> ProcessAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(signatureHeader);

        var verification = await billingProvider
            .VerifyWebhookSignatureAsync(payload, signatureHeader, cancellationToken)
            .ConfigureAwait(false);

        if (!verification.IsValid)
            return BillingWebhookProcessingResult.InvalidSignature;

        var parsed = await billingProvider.ParseWebhookEventAsync(payload, cancellationToken).ConfigureAwait(false);
        if (parsed is null)
            return BillingWebhookProcessingResult.Ignored;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var alreadyProcessed = await context.ProcessedWebhookEvents.AsNoTracking()
            .AnyAsync(e => e.ProviderEventId == parsed.ProviderEventId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyProcessed)
            return BillingWebhookProcessingResult.AlreadyProcessed;

        var outcome = BillingWebhookProcessingResult.Applied;

        // State is applied before the event is marked processed, never the other way round.
        // The reverse order turns any failure below - a transient database fault, a deadlock -
        // into a permanently lost plan change: the provider's retry would find the event
        // already recorded and skip it, so a paying customer would silently stay on Starter.
        // Every operation below is idempotent (each is a no-op when the value already
        // matches), so a retry that re-applies part of an event is harmless.
        if (!string.IsNullOrWhiteSpace(parsed.TargetUserId))
        {
            // A deleted account still gets the subscription.deleted event its own deletion
            // triggered. Creating a UserPlan for it would violate the foreign key and fail the
            // delivery on every retry, so the event is recorded and acknowledged instead.
            var userExists = await context.Users.AsNoTracking()
                .AnyAsync(u => u.Id == parsed.TargetUserId, cancellationToken)
                .ConfigureAwait(false);

            outcome = userExists
                ? await ApplyAsync(parsed, parsed.TargetUserId, cancellationToken).ConfigureAwait(false)
                : BillingWebhookProcessingResult.UnknownUser;
        }

        context.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderEventId = parsed.ProviderEventId,
            EventType = parsed.EventType,
            TargetUserId = parsed.TargetUserId,
            ProcessedAtUtc = DateTime.UtcNow,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Unique index on ProviderEventId: a concurrent delivery of the same event won
            // the race and has already applied the same idempotent changes.
            return BillingWebhookProcessingResult.AlreadyProcessed;
        }

        return outcome;
    }

    public async Task<bool> ConfirmCheckoutAsync(
        string userId,
        string checkoutSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutSessionId);

        var parsed = await billingProvider
            .GetCompletedCheckoutAsync(userId, checkoutSessionId, cancellationToken)
            .ConfigureAwait(false);
        if (parsed is null)
            return false;

        // Read live from the provider rather than delivered, so it has no event time and is not
        // recorded in the webhook ledger: the checkout's own webhooks still arrive and re-apply
        // the same state idempotently.
        await ApplyAsync(parsed, userId, cancellationToken).ConfigureAwait(false);
        return parsed.NewTier == PlanTier.Pro;
    }

    private async Task<BillingWebhookProcessingResult> ApplyAsync(
        ParsedBillingWebhookEvent parsed,
        string userId,
        CancellationToken cancellationToken)
    {
        // References only ever fill in ids, so they are safe to record whatever the event's age.
        if (parsed.BillingProviderCustomerId is not null || parsed.BillingProviderSubscriptionId is not null)
        {
            await entitlements.RecordBillingReferencesAsync(
                userId,
                parsed.BillingProviderCustomerId,
                parsed.BillingProviderSubscriptionId,
                cancellationToken).ConfigureAwait(false);
        }

        var changesState = parsed.NewTier.HasValue
            || parsed.ClearsGracePeriod
            || parsed.GracePeriodEndsAtUtc.HasValue
            || parsed.HasCancellationSchedule;
        if (!changesState)
            return BillingWebhookProcessingResult.Applied;

        // Providers deliver out of order: a subscription.updated (active) arriving after the
        // subscription.deleted that followed it must not put the user back on Pro.
        if (parsed.OccurredAtUtc.HasValue &&
            !await entitlements.TryRecordBillingEventTimeAsync(userId, parsed.OccurredAtUtc.Value, cancellationToken).ConfigureAwait(false))
        {
            return BillingWebhookProcessingResult.StaleEvent;
        }

        if (parsed.ClearsGracePeriod || parsed.GracePeriodEndsAtUtc.HasValue)
        {
            await entitlements.SetGracePeriodAsync(
                userId,
                parsed.ClearsGracePeriod ? null : parsed.GracePeriodEndsAtUtc,
                cancellationToken).ConfigureAwait(false);
        }

        // Before the tier, because a downgrade to Starter clears the schedule again.
        if (parsed.HasCancellationSchedule)
        {
            await entitlements.SetScheduledCancellationAsync(userId, parsed.CancelsAtUtc, cancellationToken).ConfigureAwait(false);
        }

        if (parsed.NewTier.HasValue)
        {
            await entitlements.SetPlanTierAsync(
                userId,
                parsed.NewTier.Value,
                parsed.PeriodEndsAtUtc,
                cancellationToken).ConfigureAwait(false);
        }

        return BillingWebhookProcessingResult.Applied;
    }
}
