namespace LearnStack.Data.Models;

/// <summary>
/// The billing plan a user is currently on. See <c>LearnStack.Billing.PlanCatalog</c> for the
/// enforced entitlement matrix behind each tier.
/// </summary>
public enum PlanTier
{
    /// <summary>Free tier: up to 20 non-archived learning resources.</summary>
    Starter,

    /// <summary>Paid tier: unlimited learning resources.</summary>
    Pro,
}
