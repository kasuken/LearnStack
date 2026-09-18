using LearnStack.Billing;
using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

/// <summary>
/// Verifies and idempotently applies billing-provider webhook deliveries. Under
/// <c>NullBillingProvider</c>, <see cref="IBillingProvider.VerifyWebhookSignatureAsync"/>
/// always fails, so this never applies state without a real provider configured - there is no
/// way for an unauthenticated request to change anyone's plan.
/// </summary>
public class BillingWebhookProcessor(
    IBillingProvider billingProvider,
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEntitlementService entitlements) : IBillingWebhookProcessor
{
    public async Task<BillingWebhookProcessingResult> ProcessAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(signatureHeader);

        var verification = await billingProvider
            .VerifyWebhookSignatureAsync(payload, signatureHeader, cancellationToken)
            .ConfigureAwait(false);

        if (!verification.IsValid)
            return BillingWebhookProcessingResult.InvalidSignature;

        var parsed = await billingProvider.ParseWebhookEventAsync(payload, cancellationToken).ConfigureAwait(false);
        if (parsed is null)
            return BillingWebhookProcessingResult.Ignored;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var alreadyProcessed = await context.ProcessedWebhookEvents.AsNoTracking()
            .AnyAsync(e => e.ProviderEventId == parsed.ProviderEventId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyProcessed)
            return BillingWebhookProcessingResult.AlreadyProcessed;

        // State is applied before the event is marked processed, never the other way round.
        // The reverse order turns any failure below - a transient database fault, a deadlock -
        // into a permanently lost plan change: the provider's retry would find the event
        // already recorded and skip it, so a paying customer would silently stay on Starter.
        // Every operation below is idempotent (each is a no-op when the value already
        // matches), so a retry that re-applies part of an event is harmless.
        if (!string.IsNullOrWhiteSpace(parsed.TargetUserId))
        {
            if (parsed.BillingProviderCustomerId is not null || parsed.BillingProviderSubscriptionId is not null)
            {
                await entitlements.RecordBillingReferencesAsync(
                    parsed.TargetUserId,
                    parsed.BillingProviderCustomerId,
                    parsed.BillingProviderSubscriptionId,
                    cancellationToken).ConfigureAwait(false);
            }

            if (parsed.ClearsGracePeriod || parsed.GracePeriodEndsAtUtc.HasValue)
            {
                await entitlements.SetGracePeriodAsync(
                    parsed.TargetUserId,
                    parsed.ClearsGracePeriod ? null : parsed.GracePeriodEndsAtUtc,
                    cancellationToken).ConfigureAwait(false);
            }

            if (parsed.NewTier.HasValue)
            {
                await entitlements.SetPlanTierAsync(
                    parsed.TargetUserId,
                    parsed.NewTier.Value,
                    parsed.PeriodEndsAtUtc,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        context.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderEventId = parsed.ProviderEventId,
            EventType = parsed.EventType,
            TargetUserId = parsed.TargetUserId,
            ProcessedAtUtc = DateTime.UtcNow,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Unique index on ProviderEventId: a concurrent delivery of the same event won
            // the race and has already applied the same idempotent changes.
            return BillingWebhookProcessingResult.AlreadyProcessed;
        }

        return BillingWebhookProcessingResult.Applied;
    }
}
