namespace LearnStack.Data.Models;

/// <summary>The billing cadence a paid plan is purchased on.</summary>
public enum BillingInterval
{
    /// <summary>Charged every month.</summary>
    Monthly,

    /// <summary>Charged once a year.</summary>
    Yearly,
}
