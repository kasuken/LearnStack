using System.ComponentModel.DataAnnotations;

namespace LearnStack.Data.Models;

/// <summary>
/// Per-user billing/subscription state: current plan tier, renewal/grace-period timestamps,
/// and the billing provider's customer/subscription ids (populated only when a real
/// <c>IBillingProvider</c> is configured). One record per user; created lazily on first access,
/// defaulting to <see cref="PlanTier.Starter"/>.
/// </summary>
public class UserPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public PlanTier Tier { get; set; } = PlanTier.Starter;

    /// <summary>When the current billing period renews. Null until a real billing provider populates it.</summary>
    public DateTime? PlanRenewsAtUtc { get; set; }

    /// <summary>
    /// When a subscription the user cancelled stops granting paid access. While set, the plan
    /// does not renew at <see cref="PlanRenewsAtUtc"/>. Null when no cancellation is scheduled.
    /// </summary>
    public DateTime? PlanCancelsAtUtc { get; set; }

    /// <summary>
    /// When a payment-failure grace period ends. While set and in the future, the user keeps
    /// paid access despite a failed charge; once it elapses, <c>EntitlementService</c> enforces
    /// Starter limits even though <see cref="Tier"/> still reads Pro, until a successful charge
    /// clears it or the provider ends the subscription.
    /// </summary>
    public DateTime? GracePeriodEndsAtUtc { get; set; }

    /// <summary>
    /// Creation time of the most recent billing webhook event whose state change was applied.
    /// Events older than this are skipped, because providers do not deliver in order.
    /// </summary>
    public DateTime? LastBillingEventAtUtc { get; set; }

    /// <summary>The billing provider's customer id. Null under <c>NullBillingProvider</c>.</summary>
    [MaxLength(200)]
    public string? BillingProviderCustomerId { get; set; }

    /// <summary>The billing provider's subscription id. Null under <c>NullBillingProvider</c>.</summary>
    [MaxLength(200)]
    public string? BillingProviderSubscriptionId { get; set; }

    public ApplicationUser? User { get; set; }
}
