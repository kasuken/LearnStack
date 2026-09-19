using LearnStack.Billing;
using LearnStack.Data.Models;

namespace LearnStack.Services;

/// <summary>
/// Enforces LearnStack's plan matrix (<see cref="PlanCatalog"/>) server-side. Every paid-only
/// action must go through here rather than being gated only in a Razor component.
/// </summary>
public interface IEntitlementService
{
    /// <summary>Whether <paramref name="userId"/> may create another non-archived learning resource right now.</summary>
    Task<EntitlementCheckResult> CanCreateResourceAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Everything the Plan &amp; usage screen needs for <paramref name="userId"/>.</summary>
    Task<PlanUsageSummaryDto> GetUsageSummaryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <paramref name="userId"/>'s plan tier, creating the <c>UserPlan</c> row if none
    /// exists yet (defaulting to Starter), and optionally records the new renewal timestamp.
    /// Not scoped to "current user": called from the internal/admin plan-change path and from
    /// the billing webhook processor, neither of which necessarily runs in the affected user's
    /// own HTTP context.
    /// </summary>
    Task SetPlanTierAsync(
        string userId,
        PlanTier tier,
        DateTime? planRenewsAtUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the billing provider's customer and/or subscription reference id for
    /// <paramref name="userId"/>, creating the <c>UserPlan</c> row if none exists yet. Does not
    /// change the plan tier or any other field. Called by the billing webhook processor when a
    /// checkout completes or a subscription is created/updated, so the portal and future
    /// webhooks can be linked back to this user.
    /// </summary>
    Task RecordBillingReferencesAsync(
        string userId,
        string? billingProviderCustomerId,
        string? billingProviderSubscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets (when <paramref name="gracePeriodEndsAtUtc"/> is non-null) or clears (when null) the
    /// payment-failure grace period end for <paramref name="userId"/>. While set and in the
    /// future, the user's Pro access is unaffected by a failed charge; the tier itself only
    /// changes via <see cref="SetPlanTierAsync"/> once the provider actually ends the subscription.
    /// </summary>
    Task SetGracePeriodAsync(
        string userId,
        DateTime? gracePeriodEndsAtUtc,
        CancellationToken cancellationToken = default);
}
