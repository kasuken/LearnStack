using LearnStack.Data.Models;

namespace LearnStack.Billing;

/// <summary>
/// Provider-agnostic billing/payment abstraction: a <c>Billing:Provider</c> config value
/// selects the implementation, <see cref="NullBillingProvider"/> is the safe no-op default
/// when none is configured, and <c>StripeBillingProvider</c> is the live implementation.
/// Adding another provider (Paddle, etc.) means implementing this interface and registering it
/// in <c>ServiceCollectionExtensions.AddLearnStackBilling</c>; nothing in the entitlement
/// system changes.
/// </summary>
public interface IBillingProvider
{
    /// <summary>
    /// Starts a hosted checkout flow for upgrading <paramref name="userId"/> to
    /// <paramref name="targetTier"/> on <paramref name="interval"/>. Defaults to
    /// <see cref="BillingInterval.Yearly"/>.
    /// </summary>
    /// <param name="accountEmail">
    /// The LearnStack account's email address, used to seed the customer the provider creates.
    /// Without it the hosted page collects an address of its own, and the customer record ends
    /// up carrying an email unrelated to the LearnStack account, so receipts and support
    /// lookups point at the wrong person. Ignored once the user has a customer record with the
    /// provider, because that customer's own email wins from then on.
    /// </param>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string userId,
        PlanTier targetTier,
        BillingInterval interval = BillingInterval.Yearly,
        string? accountEmail = null,
        CancellationToken cancellationToken = default);

    /// <summary>Starts a hosted self-service billing-portal session for <paramref name="userId"/>.</summary>
    Task<PortalSessionResult> CreatePortalSessionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Verifies an inbound webhook request's signature before its payload is trusted.</summary>
    Task<WebhookVerificationResult> VerifyWebhookSignatureAsync(
        string payload, string signatureHeader, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a signature-verified webhook payload into the fields LearnStack's entitlement
    /// model needs. Returns null for event types LearnStack does not act on.
    /// </summary>
    Task<ParsedBillingWebhookEvent?> ParseWebhookEventAsync(string payload, CancellationToken cancellationToken = default);
}
