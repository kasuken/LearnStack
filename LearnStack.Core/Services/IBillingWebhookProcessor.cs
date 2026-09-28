using LearnStack.Billing;

namespace LearnStack.Services;

/// <summary>
/// Verifies and applies inbound billing-provider webhook deliveries idempotently. The web
/// layer's controller action should do nothing but read the request and delegate here.
/// </summary>
public interface IBillingWebhookProcessor
{
    /// <summary>
    /// Verifies <paramref name="signatureHeader"/> against <paramref name="payload"/>, and if
    /// valid, applies the event's plan change exactly once per provider event id - a repeat
    /// delivery of the same id is a safe no-op.
    /// </summary>
    Task<BillingWebhookProcessingResult> ProcessAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the subscription a completed checkout created for <paramref name="userId"/>,
    /// read live from the provider when the user returns from checkout, so the upgrade shows
    /// immediately instead of after the webhook arrives. Returns true when the user is now on a
    /// paid plan; false when the session is unknown, incomplete, or belongs to someone else.
    /// </summary>
    Task<bool> ConfirmCheckoutAsync(
        string userId,
        string checkoutSessionId,
        CancellationToken cancellationToken = default);
}
