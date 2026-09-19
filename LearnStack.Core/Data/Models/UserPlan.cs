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
    /// When a payment-failure grace period ends. While set and in the future, the user keeps
    /// paid access despite a failed charge; once it elapses, a reconciliation should downgrade the plan.
    /// </summary>
    public DateTime? GracePeriodEndsAtUtc { get; set; }

    /// <summary>The billing provider's customer id. Null under <c>NullBillingProvider</c>.</summary>
    [MaxLength(200)]
    public string? BillingProviderCustomerId { get; set; }

    /// <summary>The billing provider's subscription id. Null under <c>NullBillingProvider</c>.</summary>
    [MaxLength(200)]
    public string? BillingProviderSubscriptionId { get; set; }

    public ApplicationUser? User { get; set; }
}
