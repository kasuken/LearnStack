using LearnStack.Data.Models;

namespace LearnStack.Billing;

/// <summary>Result of asking the billing provider to start a checkout flow.</summary>
/// <param name="Supported">False when no live payment provider is configured.</param>
/// <param name="RedirectUrl">The provider-hosted checkout URL, when supported.</param>
/// <param name="UnsupportedReason">A user-facing explanation when unsupported, for an honest CTA.</param>
public sealed record CheckoutSessionResult(bool Supported, string? RedirectUrl, string? UnsupportedReason);

/// <summary>Result of asking the billing provider to start a self-service billing-portal session.</summary>
public sealed record PortalSessionResult(bool Supported, string? RedirectUrl, string? UnsupportedReason);

/// <summary>Result of verifying an inbound webhook's signature.</summary>
public sealed record WebhookVerificationResult(bool IsValid, string? FailureReason);

/// <summary>A billing provider's webhook event, normalized to the fields LearnStack's entitlement model needs.</summary>
/// <param name="ProviderEventId">The provider's unique event id, used for idempotency.</param>
/// <param name="EventType">The provider's raw event type string, kept for audit.</param>
/// <param name="TargetUserId">The LearnStack user the event applies to, when the provider payload identifies one.</param>
/// <param name="NewTier">The plan tier to apply, when this event represents a plan change.</param>
/// <param name="PeriodEndsAtUtc">The new renewal/period-end timestamp, when the event carries one.</param>
/// <param name="BillingProviderCustomerId">
/// The provider's customer id to persist for <paramref name="TargetUserId"/>, when the event carries one
/// (e.g. a completed checkout). Null means "leave whatever is already stored unchanged".
/// </param>
/// <param name="BillingProviderSubscriptionId">
/// The provider's subscription id to persist for <paramref name="TargetUserId"/>, when the event carries
/// one. Null means "leave whatever is already stored unchanged".
/// </param>
/// <param name="GracePeriodEndsAtUtc">
/// When set, the payment-failure grace period end to record - the user keeps paid access until this
/// timestamp despite a failed charge. Ignored unless <paramref name="ClearsGracePeriod"/> is also false.
/// </param>
/// <param name="ClearsGracePeriod">
/// True when this event (a successful charge, or a subscription returning to an active state) means any
/// previously-recorded grace period no longer applies and should be cleared.
/// </param>
/// <param name="OccurredAtUtc">
/// When the provider created the event. Providers do not guarantee delivery order, so this is
/// compared against the last applied event for the same user to skip state changes from an
/// event older than one already applied. Null means "apply regardless of order".
/// </param>
/// <param name="HasCancellationSchedule">
/// True when the event carries the subscription's cancellation schedule, so
/// <paramref name="CancelsAtUtc"/> is to be stored even when it is null (a cancellation was withdrawn).
/// </param>
/// <param name="CancelsAtUtc">When a subscription the user has cancelled stops granting paid access. Null when none is scheduled.</param>
public sealed record ParsedBillingWebhookEvent(
    string ProviderEventId,
    string EventType,
    string? TargetUserId,
    PlanTier? NewTier,
    DateTime? PeriodEndsAtUtc,
    string? BillingProviderCustomerId = null,
    string? BillingProviderSubscriptionId = null,
    DateTime? GracePeriodEndsAtUtc = null,
    bool ClearsGracePeriod = false,
    DateTime? OccurredAtUtc = null,
    bool HasCancellationSchedule = false,
    DateTime? CancelsAtUtc = null);

/// <summary>Result of asking the billing provider to cancel every live subscription a user has.</summary>
/// <param name="Succeeded">True when nothing is left billing the user, including when there was nothing to cancel.</param>
/// <param name="FailureReason">A log-friendly explanation when cancellation failed.</param>
public sealed record SubscriptionCancellationResult(bool Succeeded, string? FailureReason = null)
{
    public static readonly SubscriptionCancellationResult Success = new(true);
}

/// <summary>The outcome of an entitlement check: a typed, user-renderable result rather than an exception.</summary>
public sealed record EntitlementCheckResult(bool IsAllowed, string? Reason = null)
{
    public static EntitlementCheckResult Allow() => new(true);

    public static EntitlementCheckResult Deny(string reason) => new(false, reason);
}

/// <summary>Outcome of processing one inbound billing webhook delivery.</summary>
/// <param name="Accepted">
/// True when the request should be acknowledged (HTTP 2xx) - including an already-processed
/// duplicate, or a recognized-but-irrelevant event type. False means signature verification
/// failed and the request should be rejected.
/// </param>
/// <param name="Reason">A short machine-readable outcome code, for logging/audit.</param>
public sealed record BillingWebhookProcessingResult(bool Accepted, string Reason)
{
    public static readonly BillingWebhookProcessingResult InvalidSignature = new(false, "invalid_signature");
    public static readonly BillingWebhookProcessingResult Ignored = new(true, "ignored_event_type");
    public static readonly BillingWebhookProcessingResult AlreadyProcessed = new(true, "already_processed");
    public static readonly BillingWebhookProcessingResult Applied = new(true, "applied");
    public static readonly BillingWebhookProcessingResult StaleEvent = new(true, "stale_event");
    public static readonly BillingWebhookProcessingResult UnknownUser = new(true, "unknown_user");
}

/// <summary>Everything the "Plan &amp; usage" screen needs: current plan, consumption, and renewal/grace timing.</summary>
/// <param name="PlanCancelsAtUtc">When a cancelled paid plan ends and reverts to Starter; while set, it does not renew.</param>
/// <param name="Tier">The tier currently enforced, which is Starter while <paramref name="PaidAccessSuspended"/> is true.</param>
/// <param name="PaidAccessSuspended">
/// True when the provider still reports a paid subscription but its payment-failure grace period has
/// elapsed, so paid entitlements are withheld until the payment succeeds or the subscription ends.
/// </param>
public sealed record PlanUsageSummaryDto(
    PlanTier Tier,
    string PlanDisplayName,
    string PlanPriceDescription,
    int ResourceCount,
    int? MaxResources,
    bool ResourceLimitReached,
    DateTime? PlanRenewsAtUtc,
    DateTime? GracePeriodEndsAtUtc,
    bool PaidAccessSuspended = false,
    DateTime? PlanCancelsAtUtc = null);
