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
    private const string LearnStackMetadataKey = "learnstack_user_id";

    // Another product (Brainy) billed through the same Stripe account: its events reach
    // LearnStack's endpoint too, validly signed with LearnStack's own endpoint secret.
    private const string OtherProductMetadataKey = "brainy_user_id";
    private const string OtherProductUserId = "brainy-user-1";
    private const string OtherProductPriceId = "price_brainy_pro";
    private const string OtherProductSubscriptionId = "sub_brainy_1";

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

    [Theory]
    [InlineData("canceled")]
    [InlineData("unpaid")]
    [InlineData("incomplete_expired")]
    [InlineData("paused")]
    public async Task ParseWebhookEventAsync_SubscriptionUpdatedToAStatusStripeNoLongerBills_MapsToStarterAndClearsGrace(string status)
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_ended", "customer.subscription.updated", status, YearlyPriceId, 1748736000));

        Assert.NotNull(parsed);
        Assert.Equal(PlanTier.Starter, parsed!.NewTier);
        Assert.True(parsed.ClearsGracePeriod);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_SubscriptionIncomplete_LinksAccountWithoutChangingTier()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_incomplete", "customer.subscription.created", "incomplete", YearlyPriceId, 1748736000));

        Assert.NotNull(parsed);
        Assert.Null(parsed!.NewTier);
        Assert.Equal(SubscriptionId, parsed.BillingProviderSubscriptionId);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_SubscriptionEvent_CarriesTheEventCreationTime()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_created_at", "customer.subscription.updated", "active", YearlyPriceId, 1748736000, created: 1751328000));

        Assert.Equal(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc), parsed!.OccurredAtUtc);
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

        var parsed = await provider.ParseWebhookEventAsync(
            InvoicePayload("evt_invoice_failed_1", "invoice.payment_failed", nextPaymentAttempt: 1751328000, linePriceId: YearlyPriceId));

        Assert.NotNull(parsed);
        Assert.Null(parsed!.NewTier);
        // Stripe's next retry (2025-07-01) plus a day for that retry's own webhook to arrive.
        Assert.Equal(new DateTime(2025, 7, 2, 0, 0, 0, DateTimeKind.Utc), parsed.GracePeriodEndsAtUtc);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_InvoicePaid_ClearsGracePeriodWithoutGrantingATier()
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

        var parsed = await provider.ParseWebhookEventAsync(
            InvoicePayload("evt_invoice_paid_1", "invoice.paid", periodEnd: 1748736000, linePriceId: YearlyPriceId));

        // Tier and renewal come from subscription events only: an invoice may be for a price
        // LearnStack does not sell, or arrive after the subscription was deleted.
        Assert.NotNull(parsed);
        Assert.Null(parsed!.NewTier);
        Assert.Null(parsed.PeriodEndsAtUtc);
        Assert.True(parsed.ClearsGracePeriod);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_InvoiceWithLearnStackSubscriptionMetadata_ResolvesUserFromTheMetadata()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            InvoicePayload("evt_invoice_metadata", "invoice.paid", periodEnd: 1748736000, subscriptionMetadataUserId: UserId));

        Assert.NotNull(parsed);
        Assert.Equal(UserId, parsed!.TargetUserId);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_InvoiceForTheStoredSubscription_ResolvesUserWithoutAConfiguredPrice()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan
            {
                UserId = UserId,
                Tier = PlanTier.Pro,
                BillingProviderCustomerId = CustomerId,
                BillingProviderSubscriptionId = SubscriptionId,
            });
            await context.SaveChangesAsync();
        }

        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            InvoicePayload("evt_invoice_stored_sub", "invoice.payment_failed", nextPaymentAttempt: 1751328000, subscriptionId: SubscriptionId));

        Assert.NotNull(parsed);
        Assert.Equal(UserId, parsed!.TargetUserId);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_CheckoutFromAnotherProduct_IsIgnored()
    {
        // client_reference_id is set by every product's checkout, so it alone must never be
        // read as a LearnStack user id (it would also fail the UserPlan foreign key).
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            CheckoutCompletedPayload("evt_other_checkout", OtherProductUserId, metadataKey: OtherProductMetadataKey));

        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("customer.subscription.created")]
    [InlineData("customer.subscription.updated")]
    [InlineData("customer.subscription.deleted")]
    public async Task ParseWebhookEventAsync_AnotherProductsSubscriptionOnALearnStackCustomer_IsIgnored(string eventType)
    {
        // Same Stripe customer as a paying LearnStack user. Before this was guarded, an unknown
        // price mapped to Starter and a deletion downgraded the LearnStack user.
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId, BillingProviderSubscriptionId = SubscriptionId });
            await context.SaveChangesAsync();
        }

        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(SubscriptionPayload(
            "evt_other_subscription",
            eventType,
            "active",
            OtherProductPriceId,
            1748736000,
            metadataUserId: OtherProductUserId,
            metadataKey: OtherProductMetadataKey,
            subscriptionId: OtherProductSubscriptionId));

        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("invoice.paid")]
    [InlineData("invoice.payment_failed")]
    public async Task ParseWebhookEventAsync_AnotherProductsInvoiceOnALearnStackCustomer_IsIgnored(string eventType)
    {
        // Before this was guarded, a failed invoice for another product put the LearnStack user
        // into a grace period, and a paid one cleared it.
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId, BillingProviderSubscriptionId = SubscriptionId });
            await context.SaveChangesAsync();
        }

        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(InvoicePayload(
            "evt_other_invoice",
            eventType,
            nextPaymentAttempt: 1751328000,
            periodEnd: 1748736000,
            subscriptionId: OtherProductSubscriptionId,
            subscriptionMetadataUserId: OtherProductUserId,
            metadataKey: OtherProductMetadataKey,
            linePriceId: OtherProductPriceId));

        Assert.Null(parsed);
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

        var fakeClient = new FakeStripeClient(type => type == typeof(StripeList<Subscription>)
            ? new StripeList<Subscription> { Data = [] }
            : new Stripe.Checkout.Session { Id = "cs_test_1", Url = "https://checkout.stripe.com/c/pay/cs_test_1" });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro, BillingInterval.Yearly, "account@example.test");

        var options = Assert.Single(fakeClient.SentOptions.OfType<Stripe.Checkout.SessionCreateOptions>());
        Assert.Equal(CustomerId, options.Customer);
        Assert.Null(options.CustomerEmail);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_AddsTheSessionIdPlaceholderToTheSuccessUrl()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var fakeClient = new FakeStripeClient(_ => new Stripe.Checkout.Session { Id = "cs_test_1", Url = "https://checkout.stripe.com/c/pay/cs_test_1" });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro, BillingInterval.Monthly);

        var options = Assert.IsType<Stripe.Checkout.SessionCreateOptions>(Assert.Single(fakeClient.SentOptions));
        Assert.Equal("https://app.example.test/Account/Manage/Plan?checkout=success&session_id={CHECKOUT_SESSION_ID}", options.SuccessUrl);
        // Already carries a checkout outcome, so it is left as configured.
        Assert.Equal("https://app.example.test/Account/Manage/Plan?checkout=cancelled", options.CancelUrl);
        Assert.Equal(MonthlyPriceId, Assert.Single(options.LineItems).Price);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WhenAlreadyOnPro_IsRefusedWithoutCallingStripe()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var fakeClient = new FakeStripeClient(_ => throw new InvalidOperationException("Should not call Stripe."));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro);

        Assert.False(result.Supported);
        Assert.Empty(fakeClient.Requests);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WhenStripeAlreadyHasALiveSubscription_IsRefused()
    {
        // The stored tier still says Starter because the first checkout's webhook has not landed.
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Starter, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var fakeClient = new FakeStripeClient(type => type == typeof(StripeList<Subscription>)
            ? new StripeList<Subscription> { Data = [new Subscription { Id = SubscriptionId, Status = "active", Metadata = LearnStackMetadata() }] }
            : throw new InvalidOperationException("Should not create a checkout session."));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro);

        Assert.False(result.Supported);
        Assert.DoesNotContain(fakeClient.Requests, r => r.Path == "/v1/checkout/sessions");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WhenTheCustomerOnlyHasAnotherProductsSubscription_OpensCheckout()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Starter, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var fakeClient = new FakeStripeClient(type => type == typeof(StripeList<Subscription>)
            ? new StripeList<Subscription>
            {
                Data = [new Subscription { Id = OtherProductSubscriptionId, Status = "active", Metadata = new() { [OtherProductMetadataKey] = OtherProductUserId } }],
            }
            : new Stripe.Checkout.Session { Id = "cs_test_1", Url = "https://checkout.stripe.com/c/pay/cs_test_1" });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CreateCheckoutSessionAsync(UserId, PlanTier.Pro);

        Assert.True(result.Supported);
    }

    [Fact]
    public async Task GetCompletedCheckoutAsync_ForTheUsersCompletedSession_MapsTheSubscriptionToPro()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var fakeClient = new FakeStripeClient(_ => CompletedCheckoutSession(UserId));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var parsed = await provider.GetCompletedCheckoutAsync(UserId, "cs_test_1");

        Assert.NotNull(parsed);
        Assert.Equal(PlanTier.Pro, parsed!.NewTier);
        Assert.Equal(CustomerId, parsed.BillingProviderCustomerId);
        Assert.Equal(SubscriptionId, parsed.BillingProviderSubscriptionId);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), parsed.PeriodEndsAtUtc);
        Assert.Null(parsed.OccurredAtUtc);
        Assert.Contains(fakeClient.Requests, r => r.Method == HttpMethod.Get && r.Path == "/v1/checkout/sessions/cs_test_1");
    }

    [Fact]
    public async Task GetCompletedCheckoutAsync_ForAnotherUsersSession_ReturnsNull()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory, stripeClient: new FakeStripeClient(_ => CompletedCheckoutSession("someone-else")));

        Assert.Null(await provider.GetCompletedCheckoutAsync(UserId, "cs_test_1"));
    }

    [Fact]
    public async Task ParseWebhookEventAsync_SubscriptionCancelledAtPeriodEnd_CarriesTheScheduledEnd()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_cancel_scheduled", "customer.subscription.updated", "active", YearlyPriceId, 1748736000, cancelAtPeriodEnd: true));

        Assert.Equal(PlanTier.Pro, parsed!.NewTier);
        Assert.True(parsed.HasCancellationSchedule);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), parsed.CancelsAtUtc);
    }

    [Fact]
    public async Task ParseWebhookEventAsync_ActiveSubscriptionWithoutCancellation_ClearsTheSchedule()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var provider = CreateProvider(factory);

        var parsed = await provider.ParseWebhookEventAsync(
            SubscriptionPayload("evt_sub_resumed", "customer.subscription.updated", "active", YearlyPriceId, 1748736000));

        Assert.True(parsed!.HasCancellationSchedule);
        Assert.Null(parsed.CancelsAtUtc);
    }

    [Fact]
    public async Task UpdateCustomerEmailAsync_WithStoredCustomer_UpdatesTheStripeCustomer()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var fakeClient = new FakeStripeClient(_ => new Customer { Id = CustomerId });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        Assert.True(await provider.UpdateCustomerEmailAsync(UserId, "new@example.test"));

        Assert.Contains(fakeClient.Requests, r => r.Method == HttpMethod.Post && r.Path == $"/v1/customers/{CustomerId}");
        Assert.Equal("new@example.test", Assert.IsType<CustomerUpdateOptions>(Assert.Single(fakeClient.SentOptions)).Email);
    }

    [Fact]
    public async Task UpdateCustomerEmailAsync_WithNoCustomer_SucceedsWithoutCallingStripe()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var fakeClient = new FakeStripeClient(_ => throw new InvalidOperationException("Should not call Stripe."));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        Assert.True(await provider.UpdateCustomerEmailAsync(UserId, "new@example.test"));
        Assert.Empty(fakeClient.Requests);
    }

    private static Stripe.Checkout.Session CompletedCheckoutSession(string clientReferenceId) => new()
    {
        Id = "cs_test_1",
        Mode = "subscription",
        Status = "complete",
        ClientReferenceId = clientReferenceId,
        CustomerId = CustomerId,
        Subscription = new Subscription
        {
            Id = SubscriptionId,
            Status = "active",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = YearlyPriceId }, CurrentPeriodEnd = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc) }],
            },
        },
    };

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

    [Fact]
    public async Task CancelSubscriptionsAsync_WithNoStoredCustomerId_SucceedsWithoutCallingStripe()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var fakeClient = new FakeStripeClient(_ => throw new InvalidOperationException("Should not call Stripe."));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CancelSubscriptionsAsync(UserId);

        Assert.True(result.Succeeded);
        Assert.Empty(fakeClient.Requests);
    }

    [Fact]
    public async Task CancelSubscriptionsAsync_CancelsEveryLiveLearnStackSubscriptionOnTheCustomer()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var fakeClient = new FakeStripeClient(type => type == typeof(StripeList<Subscription>)
            ? new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription { Id = "sub_live", Status = "active", Metadata = LearnStackMetadata() },
                    new Subscription { Id = "sub_duplicate", Status = "past_due", Metadata = LearnStackMetadata() },
                    new Subscription { Id = "sub_expired", Status = "incomplete_expired", Metadata = LearnStackMetadata() },
                    // Another product's subscription on the same customer must keep running.
                    new Subscription { Id = OtherProductSubscriptionId, Status = "active", Metadata = new() { [OtherProductMetadataKey] = OtherProductUserId } },
                ],
            }
            : new Subscription { Status = "canceled" });
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CancelSubscriptionsAsync(UserId);

        Assert.True(result.Succeeded);
        var cancelled = fakeClient.Requests.Where(r => r.Method == HttpMethod.Delete).Select(r => r.Path).ToList();
        Assert.Equal(["/v1/subscriptions/sub_live", "/v1/subscriptions/sub_duplicate"], cancelled);
    }

    [Fact]
    public async Task CancelSubscriptionsAsync_WhenStripeFails_ReportsFailure()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, BillingProviderCustomerId = CustomerId });
            await context.SaveChangesAsync();
        }

        var fakeClient = new FakeStripeClient(_ => throw new StripeException("Stripe is unavailable."));
        var provider = CreateProvider(factory, stripeClient: fakeClient);

        var result = await provider.CancelSubscriptionsAsync(UserId);

        Assert.False(result.Succeeded);
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

    private static string CheckoutCompletedPayload(
        string eventId,
        string? clientReferenceId,
        string mode = "subscription",
        string metadataKey = LearnStackMetadataKey) =>
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
              "metadata": {{MetadataJson(metadataKey, clientReferenceId)}},
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
        string? metadataUserId = UserId,
        long created = 1748000000,
        bool cancelAtPeriodEnd = false,
        string metadataKey = LearnStackMetadataKey,
        string subscriptionId = SubscriptionId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{ApiVersion}}",
          "created": {{created}},
          "type": "{{eventType}}",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "customer": "{{CustomerId}}",
              "status": "{{status}}",
              "cancel_at_period_end": {{(cancelAtPeriodEnd ? "true" : "false")}},
              "metadata": {{MetadataJson(metadataKey, metadataUserId)}},
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

    /// <summary>
    /// An invoice event in the shape Stripe's current API sends: the billed subscription (and a
    /// snapshot of its metadata) under <c>parent.subscription_details</c>, and each line's
    /// price under <c>pricing.price_details</c>.
    /// </summary>
    private static string InvoicePayload(
        string eventId,
        string eventType,
        long? nextPaymentAttempt = null,
        long? periodEnd = null,
        string subscriptionId = "sub_unrecorded",
        string? subscriptionMetadataUserId = null,
        string metadataKey = LearnStackMetadataKey,
        string linePriceId = "price_unconfigured") =>
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
              "customer": "{{CustomerId}}",
              "parent": {
                "type": "subscription_details",
                "subscription_details": {
                  "subscription": "{{subscriptionId}}",
                  "metadata": {{MetadataJson(metadataKey, subscriptionMetadataUserId)}}
                }
              },
              "lines": {
                "object": "list",
                "data": [
                  {
                    "id": "il_test_1",
                    "object": "line_item",
                    "pricing": {
                      "type": "price_details",
                      "price_details": { "price": "{{linePriceId}}", "product": "prod_test_1" }
                    }
                  }
                ]
              }{{(nextPaymentAttempt.HasValue ? $",\n              \"next_payment_attempt\": {nextPaymentAttempt.Value}" : string.Empty)}}{{(periodEnd.HasValue ? $",\n              \"period_end\": {periodEnd.Value}" : string.Empty)}}
            }
          }
        }
        """;

    private static string MetadataJson(string key, string? userId) =>
        userId is null ? "{}" : $$"""{"{{key}}": "{{userId}}"}""";

    private static Dictionary<string, string> LearnStackMetadata() => new() { [LearnStackMetadataKey] = UserId };
}
