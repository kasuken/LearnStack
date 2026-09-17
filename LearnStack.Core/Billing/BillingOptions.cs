namespace LearnStack.Billing;

/// <summary>Configuration options for the billing/payment provider.</summary>
public sealed class BillingOptions
{
    /// <summary>The configuration section name to bind from.</summary>
    public const string SectionName = "Billing";

    /// <summary>The billing provider to use. Defaults to <see cref="BillingProviderType.None"/> (no live payments).</summary>
    public BillingProviderType Provider { get; set; } = BillingProviderType.None;

    /// <summary>API key/secret for the chosen provider.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Shared secret used to verify inbound webhook signatures.</summary>
    public string? WebhookSigningSecret { get; set; }

    /// <summary>
    /// Provider price identifier for the Pro plan billed monthly. Required when
    /// <see cref="Provider"/> is <see cref="BillingProviderType.Stripe"/>: it is both the price
    /// a monthly checkout charges and the identifier inbound subscription webhooks are matched
    /// against to resolve a tier.
    /// </summary>
    public string? ProMonthlyPriceId { get; set; }

    /// <summary>Provider price identifier for the Pro plan billed yearly. Required under Stripe, for the same reasons as <see cref="ProMonthlyPriceId"/>.</summary>
    public string? ProYearlyPriceId { get; set; }

    /// <summary>
    /// Absolute URL the provider returns to after a completed checkout. Required under Stripe.
    /// Kept in configuration rather than derived from the request because webhook and
    /// background callers have no originating request to derive a host from.
    /// </summary>
    public string? CheckoutSuccessUrl { get; set; }

    /// <summary>Absolute URL the provider returns to when the user abandons checkout. Required under Stripe.</summary>
    public string? CheckoutCancelUrl { get; set; }

    /// <summary>Absolute URL the provider's self-service billing portal returns to. Required under Stripe.</summary>
    public string? PortalReturnUrl { get; set; }
}

/// <summary>Supported billing provider back-ends.</summary>
public enum BillingProviderType
{
    /// <summary>
    /// No live payment integration. Plan changes only happen through the internal/admin path
    /// (<c>IEntitlementService.SetPlanTierAsync</c>) or a manually-applied webhook test event.
    /// </summary>
    None,

    /// <summary>
    /// Live Stripe integration (<c>StripeBillingProvider</c>): hosted Checkout for upgrades,
    /// the hosted Customer Portal for self-service changes, and signature-verified webhooks
    /// that drive <see cref="Data.Models.PlanTier"/> changes. Requires <see cref="BillingOptions.ApiKey"/>,
    /// <see cref="BillingOptions.WebhookSigningSecret"/>, both price ids, and all three redirect URLs.
    /// </summary>
    Stripe,
}
