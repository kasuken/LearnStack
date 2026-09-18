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
}
