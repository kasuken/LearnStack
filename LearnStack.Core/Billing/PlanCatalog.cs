using LearnStack.Data.Models;

namespace LearnStack.Billing;

/// <summary>
/// One row of LearnStack's plan matrix: every marketed capability mapped to a value
/// <see cref="Services.EntitlementService"/> actually enforces.
/// </summary>
/// <param name="Tier">The plan tier this row describes.</param>
/// <param name="DisplayName">User-facing plan name.</param>
/// <param name="PriceDescription">User-facing price.</param>
/// <param name="MaxResources">
/// Maximum non-archived learning resources. Null means unlimited. Enforced in
/// <c>IEntitlementService.CanCreateResourceAsync</c>.
/// </param>
/// <param name="Capabilities">User-facing bullet list, shown on the Plan &amp; usage screen.</param>
public sealed record PlanDefinition(
    PlanTier Tier,
    string DisplayName,
    string PriceDescription,
    int? MaxResources,
    IReadOnlyList<string> Capabilities);

/// <summary>The plan matrix: every <see cref="PlanTier"/> mapped to its enforced entitlements.</summary>
public static class PlanCatalog
{
    public static readonly PlanDefinition Starter = new(
        Tier: PlanTier.Starter,
        DisplayName: "Starter",
        PriceDescription: "$0 / forever",
        MaxResources: 20,
        Capabilities:
        [
            "Track up to 20 learning resources",
            "Content ideas and shared groups",
            "Friends and public resource sharing",
        ]);

    public static readonly PlanDefinition Pro = new(
        Tier: PlanTier.Pro,
        DisplayName: "Pro",
        // NOTE: placeholder copy only. The real Pro price is a product decision the user
        // still needs to make and configure via Stripe (see the two configured price ids in
        // BillingOptions) - this string is not meant to assert a real price.
        PriceDescription: "$X / month (price to be finalized)",
        MaxResources: null,
        Capabilities:
        [
            "Everything in Starter",
            "Unlimited learning resources",
            "Priority support",
        ]);

    /// <summary>All plan definitions, in display order.</summary>
    public static readonly IReadOnlyList<PlanDefinition> All = [Starter, Pro];

    public static PlanDefinition Get(PlanTier tier) => tier switch
    {
        PlanTier.Starter => Starter,
        PlanTier.Pro => Pro,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown plan tier."),
    };
}
