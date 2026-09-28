using LearnStack.Common;
using LearnStack.Data.Models;
using LearnStack.Services;

namespace LearnStack.Core.Tests.Services;

public class EntitlementServiceTests
{
    private const string UserId = "user-1";

    private static Task<TestDbContextFactory> MakeFactory()
        => TestDbContextFactory.CreateAsync(UserId);

    // -----------------------------------------------------------------------
    // CanCreateResourceAsync - Starter cap
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CanCreateResourceAsync_StarterUnderCap_Allows()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        for (var i = 0; i < 19; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        var result = await entitlements.CanCreateResourceAsync(UserId);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CanCreateResourceAsync_StarterAt20NonArchivedResources_Denies()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        for (var i = 0; i < 20; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        var result = await entitlements.CanCreateResourceAsync(UserId);

        Assert.False(result.IsAllowed);
        Assert.NotNull(result.Reason);
        Assert.Contains("Starter", result.Reason);
        Assert.Contains("20", result.Reason);
    }

    [Fact]
    public async Task CanCreateResourceAsync_AfterArchivingOneOfTwenty_AllowsAgain()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        var created = new List<LearningResource>();
        for (var i = 0; i < 20; i++)
        {
            created.Add(await resources.CreateAsync(MakeResource($"Resource {i}")));
        }

        Assert.False((await entitlements.CanCreateResourceAsync(UserId)).IsAllowed);

        await resources.ToggleArchiveAsync(created[0].Id, UserId);

        var result = await entitlements.CanCreateResourceAsync(UserId);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CanCreateResourceAsync_ProTier_AlwaysAllowsEvenPastTwenty()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro);

        for (var i = 0; i < 25; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        var result = await entitlements.CanCreateResourceAsync(UserId);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CanCreateResourceAsync_ProWithElapsedGracePeriod_EnforcesStarterLimit()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro);
        for (var i = 0; i < 20; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        await entitlements.SetGracePeriodAsync(UserId, DateTime.UtcNow.AddMinutes(-1));

        Assert.False((await entitlements.CanCreateResourceAsync(UserId)).IsAllowed);

        var summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Equal(PlanTier.Starter, summary.Tier);
        Assert.True(summary.PaidAccessSuspended);
    }

    [Fact]
    public async Task CanCreateResourceAsync_ProWithinGracePeriod_KeepsProAccess()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro);
        for (var i = 0; i < 20; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        await entitlements.SetGracePeriodAsync(UserId, DateTime.UtcNow.AddDays(3));

        Assert.True((await entitlements.CanCreateResourceAsync(UserId)).IsAllowed);
        Assert.False((await entitlements.GetUsageSummaryAsync(UserId)).PaidAccessSuspended);
    }

    // -----------------------------------------------------------------------
    // Restoring archived resources counts against the limit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ToggleArchiveAsync_UnarchivingAtTheStarterLimit_IsDeniedAndLeavesTheResourceArchived()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        var archived = await resources.CreateAsync(MakeResource("Archived"));
        await resources.ToggleArchiveAsync(archived.Id, UserId);
        for (var i = 0; i < 20; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        await Assert.ThrowsAsync<PlanEntitlementDeniedException>(() => resources.ToggleArchiveAsync(archived.Id, UserId));
        Assert.True((await resources.GetByIdAsync(archived.Id, UserId))!.IsArchived);

        // Archiving is always allowed, and frees the slot the restore needs.
        var other = (await resources.GetAllAsync(UserId)).First(r => !r.IsArchived);
        await resources.ToggleArchiveAsync(other.Id, UserId);
        Assert.False(await resources.ToggleArchiveAsync(archived.Id, UserId));
    }

    [Fact]
    public async Task UpdateAsync_UnarchivingAtTheStarterLimit_IsDenied()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        var archived = await resources.CreateAsync(MakeResource("Archived"));
        await resources.ToggleArchiveAsync(archived.Id, UserId);
        for (var i = 0; i < 20; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        var edit = (await resources.GetByIdAsync(archived.Id, UserId))!;
        edit.IsArchived = false;

        await Assert.ThrowsAsync<PlanEntitlementDeniedException>(() => resources.UpdateAsync(edit, UserId));
    }

    // -----------------------------------------------------------------------
    // GetUsageSummaryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetUsageSummaryAsync_DefaultsToStarterWithNoUserPlanRow()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);

        var summary = await entitlements.GetUsageSummaryAsync(UserId);

        Assert.Equal(PlanTier.Starter, summary.Tier);
        Assert.Equal(20, summary.MaxResources);
        Assert.Equal(0, summary.ResourceCount);
        Assert.False(summary.ResourceLimitReached);
    }

    [Fact]
    public async Task GetUsageSummaryAsync_StarterAtLimit_ReportsLimitReached()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        for (var i = 0; i < 20; i++)
        {
            await resources.CreateAsync(MakeResource($"Resource {i}"));
        }

        var summary = await entitlements.GetUsageSummaryAsync(UserId);

        Assert.Equal(20, summary.ResourceCount);
        Assert.Equal(20, summary.MaxResources);
        Assert.True(summary.ResourceLimitReached);
    }

    [Fact]
    public async Task GetUsageSummaryAsync_ProTier_ReportsUnlimited()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);
        var resources = new LearningResourceService(factory, entitlements);

        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro);
        await resources.CreateAsync(MakeResource("Only resource"));

        var summary = await entitlements.GetUsageSummaryAsync(UserId);

        Assert.Equal(PlanTier.Pro, summary.Tier);
        Assert.Null(summary.MaxResources);
        Assert.Equal(1, summary.ResourceCount);
        Assert.False(summary.ResourceLimitReached);
    }

    // -----------------------------------------------------------------------
    // SetPlanTierAsync / RecordBillingReferencesAsync / SetGracePeriodAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetPlanTierAsync_DowngradeToStarter_ClearsTheRenewalDate()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);

        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await entitlements.SetPlanTierAsync(UserId, PlanTier.Starter);

        var summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Equal(PlanTier.Starter, summary.Tier);
        Assert.Null(summary.PlanRenewsAtUtc);
    }

    [Fact]
    public async Task SetPlanTierAsync_CreatesUserPlanRowWhenMissing()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);

        await entitlements.SetPlanTierAsync(UserId, PlanTier.Pro, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Equal(PlanTier.Pro, summary.Tier);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), summary.PlanRenewsAtUtc);
    }

    [Fact]
    public async Task RecordBillingReferencesAsync_StoresCustomerAndSubscriptionIds()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);

        await entitlements.RecordBillingReferencesAsync(UserId, "cus_123", "sub_123");

        await using var context = await factory.CreateDbContextAsync();
        var plan = context.UserPlans.Single(p => p.UserId == UserId);
        Assert.Equal("cus_123", plan.BillingProviderCustomerId);
        Assert.Equal("sub_123", plan.BillingProviderSubscriptionId);
    }

    [Fact]
    public async Task SetGracePeriodAsync_SetsAndClearsGracePeriod()
    {
        await using var factory = await MakeFactory();
        var entitlements = new EntitlementService(factory);

        var gracePeriodEnd = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await entitlements.SetGracePeriodAsync(UserId, gracePeriodEnd);

        var summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Equal(gracePeriodEnd, summary.GracePeriodEndsAtUtc);

        await entitlements.SetGracePeriodAsync(UserId, null);

        summary = await entitlements.GetUsageSummaryAsync(UserId);
        Assert.Null(summary.GracePeriodEndsAtUtc);
    }

    private static LearningResource MakeResource(string title) => new()
    {
        UserId = UserId,
        Title = title,
        Url = $"https://example.com/{Guid.NewGuid()}",
        ContentType = ContentType.BlogPost,
        Status = ContentStatus.ToLearn,
        Priority = Priority.Medium
    };
}
