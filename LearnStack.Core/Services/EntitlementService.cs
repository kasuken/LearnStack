using LearnStack.Billing;
using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

/// <summary>
/// Enforces the plan matrix defined in <see cref="PlanCatalog"/>. This is the only code path
/// allowed to grant or deny a paid-only action, and the only code path allowed to mutate a
/// user's <see cref="UserPlan"/>.
/// </summary>
public class EntitlementService(IDbContextFactory<ApplicationDbContext> contextFactory) : IEntitlementService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory = contextFactory;

    public async Task<EntitlementCheckResult> CanCreateResourceAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var plan = PlanCatalog.Get(await GetEffectivePlanTierAsync(userId, cancellationToken).ConfigureAwait(false));

        if (plan.MaxResources is null)
            return EntitlementCheckResult.Allow();

        var resourceCount = await CountNonArchivedResourcesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (resourceCount < plan.MaxResources.Value)
            return EntitlementCheckResult.Allow();

        return EntitlementCheckResult.Deny(
            $"Your {plan.DisplayName} plan allows up to {plan.MaxResources.Value} resources. " +
            "Archive one or upgrade to add more.");
    }

    public async Task<PlanUsageSummaryDto> GetUsageSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var userPlan = await GetOrCreateUserPlanAsync(userId, cancellationToken).ConfigureAwait(false);
        var suspended = IsPaidAccessSuspended(userPlan.Tier, userPlan.GracePeriodEndsAtUtc);
        var plan = PlanCatalog.Get(suspended ? PlanTier.Starter : userPlan.Tier);

        var resourceCount = await CountNonArchivedResourcesAsync(userId, cancellationToken).ConfigureAwait(false);

        return new PlanUsageSummaryDto(
            plan.Tier,
            plan.DisplayName,
            plan.PriceDescription,
            resourceCount,
            plan.MaxResources,
            plan.MaxResources.HasValue && resourceCount >= plan.MaxResources.Value,
            userPlan.PlanRenewsAtUtc,
            userPlan.GracePeriodEndsAtUtc,
            suspended,
            userPlan.PlanCancelsAtUtc);
    }

    public async Task SetPlanTierAsync(
        string userId,
        PlanTier tier,
        DateTime? planRenewsAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var userPlan = await GetOrCreateUserPlanAsync(context, userId, cancellationToken).ConfigureAwait(false);

        var isStarter = tier == PlanTier.Starter;
        var newRenewsAtUtc = isStarter ? null : planRenewsAtUtc ?? userPlan.PlanRenewsAtUtc;
        var newCancelsAtUtc = isStarter ? null : userPlan.PlanCancelsAtUtc;
        if (userPlan.Tier == tier && userPlan.PlanRenewsAtUtc == newRenewsAtUtc && userPlan.PlanCancelsAtUtc == newCancelsAtUtc)
            return;

        userPlan.Tier = tier;
        userPlan.PlanRenewsAtUtc = newRenewsAtUtc;
        userPlan.PlanCancelsAtUtc = newCancelsAtUtc;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordBillingReferencesAsync(
        string userId,
        string? billingProviderCustomerId,
        string? billingProviderSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var userPlan = await GetOrCreateUserPlanAsync(context, userId, cancellationToken).ConfigureAwait(false);

        var changed = false;
        if (billingProviderCustomerId is not null && userPlan.BillingProviderCustomerId != billingProviderCustomerId)
        {
            userPlan.BillingProviderCustomerId = billingProviderCustomerId;
            changed = true;
        }

        if (billingProviderSubscriptionId is not null && userPlan.BillingProviderSubscriptionId != billingProviderSubscriptionId)
        {
            userPlan.BillingProviderSubscriptionId = billingProviderSubscriptionId;
            changed = true;
        }

        if (changed)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetScheduledCancellationAsync(
        string userId,
        DateTime? cancelsAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var userPlan = await GetOrCreateUserPlanAsync(context, userId, cancellationToken).ConfigureAwait(false);
        if (userPlan.PlanCancelsAtUtc == cancelsAtUtc)
            return;

        userPlan.PlanCancelsAtUtc = cancelsAtUtc;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryRecordBillingEventTimeAsync(
        string userId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var userPlan = await GetOrCreateUserPlanAsync(context, userId, cancellationToken).ConfigureAwait(false);

        if (userPlan.LastBillingEventAtUtc > occurredAtUtc)
            return false;

        if (userPlan.LastBillingEventAtUtc != occurredAtUtc)
        {
            userPlan.LastBillingEventAtUtc = occurredAtUtc;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public async Task SetGracePeriodAsync(
        string userId,
        DateTime? gracePeriodEndsAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var userPlan = await GetOrCreateUserPlanAsync(context, userId, cancellationToken).ConfigureAwait(false);
        if (userPlan.GracePeriodEndsAtUtc == gracePeriodEndsAtUtc)
            return;

        userPlan.GracePeriodEndsAtUtc = gracePeriodEndsAtUtc;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlanTier> GetEffectivePlanTierAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var plan = await context.UserPlans.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Tier, p.GracePeriodEndsAtUtc })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // No row yet means the user has never had a plan change recorded: default Starter.
        if (plan is null)
            return PlanTier.Starter;

        return IsPaidAccessSuspended(plan.Tier, plan.GracePeriodEndsAtUtc) ? PlanTier.Starter : plan.Tier;
    }

    /// <summary>
    /// A paid tier whose payment-failure grace period has elapsed. Evaluated on read rather than
    /// by a background job, so the downgrade takes effect the moment the grace period ends and
    /// reverses itself as soon as a successful charge clears the grace period.
    /// </summary>
    private static bool IsPaidAccessSuspended(PlanTier tier, DateTime? gracePeriodEndsAtUtc) =>
        tier != PlanTier.Starter && gracePeriodEndsAtUtc <= DateTime.UtcNow;

    private async Task<UserPlan> GetOrCreateUserPlanAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await GetOrCreateUserPlanAsync(context, userId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<UserPlan> GetOrCreateUserPlanAsync(
        ApplicationDbContext context, string userId, CancellationToken cancellationToken)
    {
        var userPlan = await context.UserPlans
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (userPlan is not null)
            return userPlan;

        userPlan = new UserPlan { UserId = userId, Tier = PlanTier.Starter };
        context.UserPlans.Add(userPlan);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return userPlan;
    }

    private async Task<int> CountNonArchivedResourcesAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.LearningResources
            .CountAsync(r => r.UserId == userId && !r.IsArchived, cancellationToken)
            .ConfigureAwait(false);
    }
}
