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

        var plan = PlanCatalog.Get(await GetPlanTierAsync(userId, cancellationToken).ConfigureAwait(false));

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
        var plan = PlanCatalog.Get(userPlan.Tier);

        var resourceCount = await CountNonArchivedResourcesAsync(userId, cancellationToken).ConfigureAwait(false);

        return new PlanUsageSummaryDto(
            plan.Tier,
            plan.DisplayName,
            plan.PriceDescription,
            resourceCount,
            plan.MaxResources,
            plan.MaxResources.HasValue && resourceCount >= plan.MaxResources.Value,
            userPlan.PlanRenewsAtUtc,
            userPlan.GracePeriodEndsAtUtc);
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

        var tierUnchanged = userPlan.Tier == tier;
        userPlan.Tier = tier;
        if (planRenewsAtUtc.HasValue)
            userPlan.PlanRenewsAtUtc = planRenewsAtUtc;

        if (tierUnchanged && !planRenewsAtUtc.HasValue)
            return;

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

    private async Task<PlanTier> GetPlanTierAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var tier = await context.UserPlans.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (PlanTier?)p.Tier)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // No row yet means the user has never had a plan change recorded: default Starter.
        return tier ?? PlanTier.Starter;
    }

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
