using LearnStack.Billing;
using LearnStack.Core.Tests.Fakes;
using LearnStack.Data.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using PlanTier = LearnStack.Data.Models.PlanTier;

namespace LearnStack.Core.Tests.Billing;

public class StripeBillingProviderTests
{
    private const string WebhookSecret = "whsec_test_secret_only_used_locally";
    private const string UserId = "stripe-user-1";
    private const string CustomerId = "cus_test_123";
    private const string SubscriptionId = "sub_test_123";
    private const string MonthlyPriceId = "price_monthly_test";
    private const string YearlyPriceId = "price_yearly_test";

    private static readonly string ApiVersion = (string)typeof(StripeConfiguration).Assembly
        .GetType("Stripe.ApiVersion")!
        .GetField(
            "Current",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(null)!;

    private static BillingOptions CreateOptions() => new()
    {
        Provider = BillingProviderType.Stripe,
        ApiKey = "sk_test_fake_key_never_sent_anywhere",
        WebhookSigningSecret = WebhookSecret,
        ProMonthlyPriceId = MonthlyPriceId,
        ProYearlyPriceId = YearlyPriceId,
        CheckoutSuccessUrl = "https://app.example.test/Account/Manage/Plan?checkout=success",
        CheckoutCancelUrl = "https://app.example.test/Account/Manage/Plan?checkout=cancelled",
        PortalReturnUrl = "https://app.example.test/Account/Manage/Plan",
    };

    private static StripeBillingProvider CreateProvider(
        TestDbContextFactory factory,
        IStripeClient? stripeClient = null,
        BillingOptions? options = null) =>
        new(
            Options.Create(options ?? CreateOptions()),
            factory,
            NullLogger<StripeBillingProvider>.Instance,
            stripeClient);

    [Fact]
    public async Task VerifyWebhookSignatureAsync_WithValidSignature_IsValid()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);
        var payload = MinimalEventJson("evt_1", "checkout.session.completed");

        var result = await provider.VerifyWebhookSignatureAsync(payload, EventUtility.GenerateSignatureHeader(payload, WebhookSecret));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_WithSignatureForADifferentPayload_IsRejected()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);
        var signedPayload = MinimalEventJson("evt_1", "checkout.session.completed");
        var signature = EventUtility.GenerateSignatureHeader(signedPayload, WebhookSecret);

        var result = await provider.VerifyWebhookSignatureAsync(MinimalEventJson("evt_1", "customer.subscription.deleted"), signature);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_WithMissingSignatureHeader_IsRejected()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var result = await provider.VerifyWebhookSignatureAsync(MinimalEventJson("evt_1", "checkout.session.completed"), "");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_WithNoSigningSecretConfigured_IsRejected()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var options = CreateOptions();
        options.WebhookSigningSecret = null;
        var provider = CreateProvider(factory, options: options);
        var payload = MinimalEventJson("evt_1", "checkout.session.completed");

        var result = await provider.VerifyWebhookSignatureAsync(payload, EventUtility.GenerateSignatureHeader(payload, WebhookSecret));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_CheckoutSessionCompleted_LinksAccountWithoutGrantingATier()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(CheckoutCompletedPayload("evt_checkout_1", UserId));

        Assert.NotNull(parsed);
        Assert.Equal(UserId, parsed!.TargetUserId);
        Assert.Null(parsed.NewTier);
        Assert.Equal(CustomerId, parsed.BillingProviderCustomerId);
        Assert.Equal(SubscriptionId, parsed.BillingProviderSubscriptionId);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_CheckoutSessionCompletedInPaymentMode_IsIgnored()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(CheckoutCompletedPayload("evt_checkout_2", UserId, "payment"));

        Assert.Null(parsed);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_SubscriptionUpdatedActive_MapsToProWithPeriodEndAndClearsGrace()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_updated_1", "customer.subscription.updated", "active", YearlyPriceId, 1748736000));

        Assert.NotNull(parsed);
        Assert.Equal(PlanTier.Pro, parsed!.NewTier);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), parsed.PeriodEndsAtUtc);
        Assert.True(parsed.ClearsGracePeriod);
        Assert.Equal(SubscriptionId, parsed.BillingProviderSubscriptionId);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_SubscriptionUpdatedPastDue_KeepsProWithoutClearingGrace()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_updated_2", "customer.subscription.updated", "past_due", MonthlyPriceId, 1751328000));

        Assert.NotNull(parsed);
        Assert.Equal(PlanTier.Pro, parsed!.NewTier);
        Assert.Equal(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc), parsed.PeriodEndsAtUtc);
        Assert.False(parsed.ClearsGracePeriod);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_SubscriptionDeleted_MapsToStarterDowngrade()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_deleted_1", "customer.subscription.deleted", "canceled", YearlyPriceId, 1748736000));

        Assert.NotNull(parsed);
        Assert.Equal(PlanTier.Starter, parsed!.NewTier);
        Assert.True(parsed.ClearsGracePeriod);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_UnconfiguredPrice_DoesNotGrantPro()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_unknown_price", "customer.subscription.updated", "active", "price_not_configured", 1748736000));

        Assert.NotNull(parsed);
        Assert.Equal(PlanTier.Starter, parsed!.NewTier);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_WithoutMetadata_ResolvesUserFromStoredCustomerId()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_no_metadata", "customer.subscription.updated", "active", YearlyPriceId, 1748736000, metadataUserId: null));

        Assert.NotNull(parsed);
        Assert.Equal(UserId, parsed!.TargetUserId);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_WithUnknownCustomerAndNoMetadata_IsIgnored()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_orphan", "customer.subscription.updated", "active", YearlyPriceId, 1748736000, metadataUserId: null));

        Assert.Null(parsed);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_InvoicePaymentFailed_ResolvesUserByCustomerIdAndSetsGracePeriod()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(InvoicePayload("evt_invoice_failed_1", "invoice.payment_failed", nextPaymentAttempt: 1751328000));

        Assert.NotNull(parsed);
        Assert.Null(parsed!.NewTier);
        Assert.Equal(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc), parsed.GracePeriodEndsAtUtc);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_InvoicePaid_ResolvesUserAndClearsGracePeriod()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan
            {
                UserId = UserId,
                Tier = PlanTier.Pro,
                BillingProviderCustomerId = CustomerId,
                GracePeriodEndsAtUtc = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await context.SaveChangesAsync();
        }

        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(InvoicePayload("evt_invoice_paid_1", "invoice.paid", periodEnd: 1748736000));

        Assert.NotNull(parsed);
        Assert.Equal(PlanTier.Pro, parsed!.NewTier);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), parsed.PeriodEndsAtUtc);
        Assert.True(parsed.ClearsGracePeriod);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_UnrecognizedEventType_ReturnsNull()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(MinimalEventJson("evt_x", "customer.created"));

        Assert.Null(parsed);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ForStarter_IsUnsupportedAndNeverCallsStripe()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var fakeClient = new FakeStripeClient(_ => throw new InvalidOperationException("Should not call Stripe."));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Starter);

        Assert.False(result.Supported);
        Assert.Null(result.RedirectUrl);
        Assert.Empty(fakeClient.Requests);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ForPro_ReturnsStripesHostedCheckoutUrl()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        const string checkoutUrl = "https://checkout.stripe.com/c/pay/cs_test_1";
        var fakeClient = new FakeStripeClient(_ => new Stripe.Checkout.Session { Id = "cs_test_1", Url = checkoutUrl });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro);

        Assert.True(result.Supported);
        Assert.Equal(checkoutUrl, result.RedirectUrl);
        Assert.Contains(fakeClient.Requests, r => r.Method == HttpMethod.Post && r.Path == "/v1/checkout/sessions");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ForAFirstPurchase_SeedsTheCustomerWithTheAccountEmail()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var fakeClient = new FakeStripeClient(_ => new Stripe.Checkout.Session { Id = "cs_test_1", Url = "https://checkout.stripe.com/c/pay/cs_test_1" });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro, BillingInterval.Yearly, "account@example.test");

        var sent = Assert.Single(fakeClient.SentOptions);
        var options = Assert.IsType<Stripe.Checkout.SessionCreateOptions>(sent);
        Assert.Equal("account@example.test", options.CustomerEmail);
        Assert.Null(options.Customer);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ForAnExistingCustomer_SendsNoEmailAlongsideTheCustomerId()
    {
        // Stripe rejects customer and customer_email together, so an account email must be
        // dropped once the user has a customer record rather than sent with it.
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Starter, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var fakeClient = new FakeStripeClient(_ => new Stripe.Checkout.Session { Id = "cs_test_1", Url = "https://checkout.stripe.com/c/pay/cs_test_1" });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro, BillingInterval.Yearly, "account@example.test");

        var sent = Assert.Single(fakeClient.SentOptions);
        var options = Assert.IsType<Stripe.Checkout.SessionCreateOptions>(sent);
        Assert.Equal(CustomerId, options.Customer);
        Assert.Null(options.CustomerEmail);
    }

    [Fact]
    public async Task CreatePortalSessionAsync_WithNoStoredCustomerId_IsUnsupportedAndNeverCallsStripe()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var fakeClient = new FakeStripeClient(_ => throw new InvalidOperationException("Should not call Stripe."));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CreatePortalSessionAsync(UserId);

        Assert.False(result.Supported);
        Assert.Null(result.RedirectUrl);
        Assert.Empty(fakeClient.Requests);
    }

    [Fact]
    public async Task CreatePortalSessionAsync_WithStoredCustomerId_ReturnsStripesHostedPortalUrl()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        const string portalUrl = "https://billing.stripe.com/p/session/test_1";
        var fakeClient = new FakeStripeClient(_ => new Stripe.BillingPortal.Session { Id = "bps_1", Url = portalUrl });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CreatePortalSessionAsync(UserId);

        Assert.True(result.Supported);
        Assert.Equal(portalUrl, result.RedirectUrl);
        Assert.Contains(fakeClient.Requests, r => r.Method == HttpMethod.Post && r.Path == "/v1/billing_portal/sessions");
    }

    private static string MinimalEventJson(string id, string type) =>
        $$"""
        {
          "id": "{{id}}",
          "object": "event",
          "api_version": "{{ApiVersion}}",
          "type": "{{type}}",
          "data": { "object": { "id": "obj_1", "object": "customer" } }
        }
        """;

    private static string CheckoutCompletedPayload(string eventId, string? clientReferenceId, string mode = "subscription") =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{ApiVersion}}",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_1",
              "object": "checkout.session",
              "mode": "{{mode}}",
              "client_reference_id": {{(clientReferenceId is null ? "null" : $"\"{clientReferenceId}\"")}},
              "customer": "{{CustomerId}}",
              "subscription": "{{SubscriptionId}}"
            }
          }
        }
        """;

    private static string SubscriptionPayload(
        string eventId,
        string eventType,
        string status,
        string priceId,
        long currentPeriodEnd,
        string? metadataUserId = UserId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{ApiVersion}}",
          "type": "{{eventType}}",
          "data": {
            "object": {
              "id": "{{SubscriptionId}}",
              "object": "subscription",
              "customer": "{{CustomerId}}",
              "status": "{{status}}",
              "metadata": {{(metadataUserId is null ? "{}" : $$"""{"learnstack_user_id": "{{metadataUserId}}"}""")}},
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_test_1",
                    "object": "subscription_item",
                    "current_period_end": {{currentPeriodEnd}},
                    "price": { "id": "{{priceId}}", "object": "price" }
                  }
                ]
              }
            }
          }
        }
        """;

    private static string InvoicePayload(string eventId, string eventType, long? nextPaymentAttempt = null, long? periodEnd = null) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{ApiVersion}}",
          "type": "{{eventType}}",
          "data": {
            "object": {
              "id": "in_test_1",
              "object": "invoice",
              "customer": "{{CustomerId}}"{{(nextPaymentAttempt.HasValue ? $",\n              \"next_payment_attempt\": {nextPaymentAttempt.Value}" : string.Empty)}}{{(periodEnd.HasValue ? $",\n              \"period_end\": {periodEnd.Value}" : string.Empty)}}
            }
          }
        }
        """;
}
