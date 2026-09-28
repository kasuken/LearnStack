using LearnStack.Billing;
using LearnStack.Data.Models;
using LearnStack.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LearnStack.Core.Tests.Services;

public class BillingWebhookProcessorTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task ProcessAsync_InvalidSignature_RejectsWithoutTouchingTheDatabase()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var billingProvider = new Mock<IBillingProvider>();
        billingProvider
            .Setup(p => p.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookVerificationResult(false, "bad signature"));

        var entitlements = new EntitlementService(factory);
        var processor = new BillingWebhookProcessor(billingProvider.Object, factory, entitlements);

        var result = await processor.ProcessAsync("payload", "bad-signature");

        Assert.False(result.Accepted);
        Assert.Equal("invalid_signature", result.Reason);

        await using var context = await factory.CreateDbContextAsync();
        Assert.Empty(context.ProcessedWebhookEvents);
        Assert.Empty(context.UserPlans);

        billingProvider.Verify(
            p => p.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_UnrecognizedEventType_IsIgnoredAndRecordsNothing()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var billingProvider = new Mock<IBillingProvider>();
        billingProvider
            .Setup(p => p.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookVerificationResult(true, null));
        billingProvider
            .Setup(p => p.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParsedBillingWebhookEvent?)null);

        var entitlements = new EntitlementService(factory);
        var processor = new BillingWebhookProcessor(billingProvider.Object, factory, entitlements);

        var result = await processor.ProcessAsync("payload", "sig");

        Assert.True(result.Accepted);
        Assert.Equal("ignored_event_type", result.Reason);

        await using var context = await factory.CreateDbContextAsync();
        Assert.Empty(context.ProcessedWebhookEvents);
    }

    [Fact]
    public async Task ProcessAsync_NewEvent_AppliesThePlanChangeAndRecordsTheEvent()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var parsed = new ParsedBillingWebhookEvent(
            ProviderEventId: "evt_1",
            EventType: "invoice.paid",
            TargetUserId: UserId,
            NewTier: PlanTier.Pro,
            PeriodEndsAtUtc: new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            BillingProviderCustomerId: "cus_1",
            BillingProviderSubscriptionId: "sub_1",
            ClearsGracePeriod: true);

        var billingProvider = new Mock<IBillingProvider>();
        billingProvider
            .Setup(p => p.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookVerificationResult(true, null));
        billingProvider
            .Setup(p => p.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsed);

        var entitlements = new EntitlementService(factory);
        var processor = new BillingWebhookProcessor(billingProvider.Object, factory, entitlements);

        var result = await processor.ProcessAsync("payload", "sig");

        Assert.True(result.Accepted);
        Assert.Equal("applied", result.Reason);

        var summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Equal(PlanTier.Pro, summary.Tier);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), summary.PlanRenewsAtUtc);
        Assert.Null(summary.GracePeriodEndsAtUtc);

        await using var context = await factory.CreateDbContextAsync();
        var processedEvent = Assert.Single(context.ProcessedWebhookEvents);
        Assert.Equal("evt_1", processedEvent.ProviderEventId);
    }

    [Fact]
    public async Task ProcessAsync_RetriedDeliveryOfTheSameEventId_IsANoOp()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var parsed = new ParsedBillingWebhookEvent(
            ProviderEventId: "evt_1",
            EventType: "customer.subscription.deleted",
            TargetUserId: UserId,
            NewTier: PlanTier.Starter,
            PeriodEndsAtUtc: null,
            ClearsGracePeriod: true);

        var billingProvider = new Mock<IBillingProvider>();
        billingProvider
            .Setup(p => p.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookVerificationResult(true, null));
        billingProvider
            .Setup(p => p.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsed);

        var entitlements = new EntitlementService(factory);
        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro);

        var processor = new BillingWebhookProcessor(billingProvider.Object, factory, entitlements);

        var first = await processor.ProcessAsync("payload", "sig");
        Assert.Equal("applied", first.Reason);

        // The subscription-deleted event downgraded the user to Starter. If the retried
        // delivery re-applied the event, nothing would change here anyway (both are Starter),
        // so also flip the user back to Pro to prove the retry truly short-circuits rather
        // than happening to be a harmless no-op.
        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro);

        var second = await processor.ProcessAsync("payload", "sig");

        Assert.True(second.Accepted);
        Assert.Equal("already_processed", second.Reason);

        var summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Equal(PlanTier.Pro, summary.Tier);

        await using var context = await factory.CreateDbContextAsync();
        Assert.Single(context.ProcessedWebhookEvents);
    }

    [Fact]
    public async Task ProcessAsync_EventOlderThanTheLastAppliedOne_SkipsTheStateChangeButRecordsTheEvent()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var deletedAt = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Starter, LastBillingEventAtUtc = deletedAt });
            await context.SaveChangesAsync();
        }

        // A subscription.updated (active) created before the subscription.deleted that was
        // already applied, but delivered after it.
        var processor = CreateProcessor(factory, new ParsedBillingWebhookEvent(
            ProviderEventId: "evt_late_update",
            EventType: "customer.subscription.updated",
            TargetUserId: UserId,
            NewTier: PlanTier.Pro,
            PeriodEndsAtUtc: new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            ClearsGracePeriod: true,
            OccurredAtUtc: deletedAt.AddMinutes(-5)));

        var result = await processor.ProcessAsync("payload", "sig");

        Assert.True(result.Accepted);
        Assert.Equal("stale_event", result.Reason);

        await using var verify = await factory.CreateDbContextAsync();
        var plan = await verify.UserPlans.SingleAsync();
        Assert.Equal(PlanTier.Starter, plan.Tier);
        Assert.Equal(deletedAt, plan.LastBillingEventAtUtc);
        Assert.Single(verify.ProcessedWebhookEvents);
    }

    [Fact]
    public async Task ProcessAsync_NewerEvent_AppliesAndAdvancesTheLastEventTime()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var occurredAt = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.UserPlans.Add(new UserPlan { UserId = UserId, Tier = PlanTier.Pro, LastBillingEventAtUtc = occurredAt.AddDays(-1) });
            await context.SaveChangesAsync();
        }

        var processor = CreateProcessor(factory, new ParsedBillingWebhookEvent(
            ProviderEventId: "evt_deleted",
            EventType: "customer.subscription.deleted",
            TargetUserId: UserId,
            NewTier: PlanTier.Starter,
            PeriodEndsAtUtc: null,
            ClearsGracePeriod: true,
            OccurredAtUtc: occurredAt));

        var result = await processor.ProcessAsync("payload", "sig");

        Assert.Equal("applied", result.Reason);

        await using var verify = await factory.CreateDbContextAsync();
        var plan = await verify.UserPlans.SingleAsync();
        Assert.Equal(PlanTier.Starter, plan.Tier);
        Assert.Equal(occurredAt, plan.LastBillingEventAtUtc);
    }

    [Fact]
    public async Task ProcessAsync_EventForADeletedAccount_IsAcknowledgedWithoutCreatingAPlan()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var processor = CreateProcessor(factory, new ParsedBillingWebhookEvent(
            ProviderEventId: "evt_after_deletion",
            EventType: "customer.subscription.deleted",
            TargetUserId: "deleted-user",
            NewTier: PlanTier.Starter,
            PeriodEndsAtUtc: null,
            BillingProviderCustomerId: "cus_gone",
            ClearsGracePeriod: true));

        var result = await processor.ProcessAsync("payload", "sig");

        Assert.True(result.Accepted);
        Assert.Equal("unknown_user", result.Reason);

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(verify.UserPlans);
        Assert.Single(verify.ProcessedWebhookEvents);
    }

    [Fact]
    public async Task ProcessAsync_ScheduledCancellation_IsStoredAndClearedByTheDowngrade()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var cancelsAt = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        await CreateProcessor(factory, new ParsedBillingWebhookEvent(
            ProviderEventId: "evt_cancel_scheduled",
            EventType: "customer.subscription.updated",
            TargetUserId: UserId,
            NewTier: PlanTier.Pro,
            PeriodEndsAtUtc: cancelsAt,
            ClearsGracePeriod: true,
            HasCancellationSchedule: true,
            CancelsAtUtc: cancelsAt)).ProcessAsync("payload", "sig");

        var entitlements = new EntitlementService(factory);
        Assert.Equal(cancelsAt, (await entitlements.GetUsageSummaryAsync(UserId)).PlanCancelsAtUtc);

        await CreateProcessor(factory, new ParsedBillingWebhookEvent(
            ProviderEventId: "evt_ended",
            EventType: "customer.subscription.deleted",
            TargetUserId: UserId,
            NewTier: PlanTier.Starter,
            PeriodEndsAtUtc: null,
            ClearsGracePeriod: true)).ProcessAsync("payload", "sig");

        var summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Equal(PlanTier.Starter, summary.Tier);
        Assert.Null(summary.PlanCancelsAtUtc);
        Assert.Null(summary.PlanRenewsAtUtc);
    }

    [Fact]
    public async Task ConfirmCheckoutAsync_AppliesTheLiveSubscriptionWithoutUsingTheWebhookLedger()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var billingProvider = new Mock<IBillingProvider>();
        billingProvider
            .Setup(p => p.GetCompletedCheckoutAsync(UserId, "cs_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedBillingWebhookEvent(
                ProviderEventId: "cs_1",
                EventType: "checkout.session.returned",
                TargetUserId: UserId,
                NewTier: PlanTier.Pro,
                PeriodEndsAtUtc: new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                BillingProviderCustomerId: "cus_1",
                BillingProviderSubscriptionId: "sub_1",
                ClearsGracePeriod: true,
                HasCancellationSchedule: true));
        var processor = new BillingWebhookProcessor(billingProvider.Object, factory, new EntitlementService(factory));

        Assert.True(await processor.ConfirmCheckoutAsync(UserId, "cs_1"));

        await using var context = await factory.CreateDbContextAsync();
        var plan = await context.UserPlans.SingleAsync();
        Assert.Equal(PlanTier.Pro, plan.Tier);
        Assert.Equal("cus_1", plan.BillingProviderCustomerId);
        Assert.Null(plan.LastBillingEventAtUtc);
        Assert.Empty(context.ProcessedWebhookEvents);
    }

    [Fact]
    public async Task ConfirmCheckoutAsync_WhenTheSessionIsNotConfirmed_ChangesNothing()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var billingProvider = new Mock<IBillingProvider>();
        billingProvider
            .Setup(p => p.GetCompletedCheckoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParsedBillingWebhookEvent?)null);
        var processor = new BillingWebhookProcessor(billingProvider.Object, factory, new EntitlementService(factory));

        Assert.False(await processor.ConfirmCheckoutAsync(UserId, "cs_forged"));

        await using var context = await factory.CreateDbContextAsync();
        Assert.Empty(context.UserPlans);
    }

    private static BillingWebhookProcessor CreateProcessor(TestDbContextFactory factory, ParsedBillingWebhookEvent parsed)
    {
        var billingProvider = new Mock<IBillingProvider>();
        billingProvider
            .Setup(p => p.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookVerificationResult(true, null));
        billingProvider
            .Setup(p => p.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsed);

        return new BillingWebhookProcessor(billingProvider.Object, factory, new EntitlementService(factory));
    }
}
