using LearnStack.Data.Models;
using LearnStack.Services;

namespace LearnStack.Core.Tests.Services;

public class LearningResourceServiceTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    // Both test user IDs seeded in every factory so FK constraints are satisfied.
    private static Task<TestDbContextFactory> MakeFactory()
        => TestDbContextFactory.CreateAsync(UserId, OtherUserId);

    // -----------------------------------------------------------------------
    // CreateAsync / GetAllAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_StoresAndReturnsResource()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        var resource = await svc.CreateAsync(new LearningResource
        {
            UserId = UserId,
            Title = "Test",
            Url = "https://example.com",
            ContentType = ContentType.BlogPost,
            Status = ContentStatus.ToLearn,
            Priority = Priority.Medium
        });

        Assert.True(resource.Id > 0);
        var fetched = await svc.GetByIdAsync(resource.Id, UserId);
        Assert.NotNull(fetched);
        Assert.Equal("Test", fetched.Title);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyResourcesForOwner()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        await svc.CreateAsync(MakeResource(UserId, "A"));
        await svc.CreateAsync(MakeResource(OtherUserId, "B"));

        var results = await svc.GetAllAsync(UserId);

        Assert.Single(results);
        Assert.Equal("A", results[0].Title);
    }

    // -----------------------------------------------------------------------
    // UrlExistsAsync — normalization
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UrlExistsAsync_WhenExactUrlExists_ReturnsTrue()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        await svc.CreateAsync(MakeResource(UserId, url: "https://example.com/page"));

        Assert.True(await svc.UrlExistsAsync(UserId, "https://example.com/page"));
    }

    [Fact]
    public async Task UrlExistsAsync_WhenUrlDiffersOnlyInCase_ReturnsTrue()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        await svc.CreateAsync(MakeResource(UserId, url: "https://EXAMPLE.COM/page"));

        Assert.True(await svc.UrlExistsAsync(UserId, "https://example.com/page"));
    }

    [Fact]
    public async Task UrlExistsAsync_WhenUrlDiffersOnlyInTrailingSlash_ReturnsTrue()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        await svc.CreateAsync(MakeResource(UserId, url: "https://example.com/page/"));

        Assert.True(await svc.UrlExistsAsync(UserId, "https://example.com/page"));
    }

    [Fact]
    public async Task UrlExistsAsync_WhenUrlBelongsToDifferentUser_ReturnsFalse()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        await svc.CreateAsync(MakeResource(OtherUserId, url: "https://example.com/page"));

        Assert.False(await svc.UrlExistsAsync(UserId, "https://example.com/page"));
    }

    [Fact]
    public async Task UrlExistsAsync_WhenExcludeIdMatchesExistingResource_ReturnsFalse()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        var existing = await svc.CreateAsync(MakeResource(UserId, url: "https://example.com/page"));

        Assert.False(await svc.UrlExistsAsync(UserId, "https://example.com/page", excludeResourceId: existing.Id));
    }

    // -----------------------------------------------------------------------
    // DeleteAsync — ownership guard
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_DoesNotDeleteResourceOwnedByAnotherUser()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        var resource = await svc.CreateAsync(MakeResource(OtherUserId, "X"));

        await svc.DeleteAsync(resource.Id, UserId); // wrong owner

        var stillExists = await svc.GetByIdAsync(resource.Id, OtherUserId);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOwnedResource()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        var resource = await svc.CreateAsync(MakeResource(UserId, "X"));
        await svc.DeleteAsync(resource.Id, UserId);

        Assert.Null(await svc.GetByIdAsync(resource.Id, UserId));
    }

    // -----------------------------------------------------------------------
    // ToggleArchiveAsync / TogglePublicAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ToggleArchiveAsync_FlipsIsArchivedFlag()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        var resource = await svc.CreateAsync(MakeResource(UserId));
        Assert.False(resource.IsArchived);

        var archived = await svc.ToggleArchiveAsync(resource.Id, UserId);
        Assert.True(archived);

        var unarchived = await svc.ToggleArchiveAsync(resource.Id, UserId);
        Assert.False(unarchived);
    }

    [Fact]
    public async Task TogglePublicAsync_FlipsIsPublicFlag()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        var resource = await svc.CreateAsync(MakeResource(UserId));
        Assert.False(resource.IsPublic);

        var madePublic = await svc.TogglePublicAsync(resource.Id, UserId);
        Assert.True(madePublic);
    }

    // -----------------------------------------------------------------------
    // GetPublicResourcesByUserIdAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPublicResourcesByUserIdAsync_ReturnsOnlyPublicNonArchivedItems()
    {
        await using var factory = await MakeFactory();
        var svc = new LearningResourceService(factory);

        var pub = await svc.CreateAsync(MakeResource(UserId, "Public", isPublic: true));
        await svc.CreateAsync(MakeResource(UserId, "Private"));
        await svc.CreateAsync(MakeResource(UserId, "ArchivedPublic", isPublic: true, isArchived: true));

        var results = await svc.GetPublicResourcesByUserIdAsync(UserId);

        Assert.Single(results);
        Assert.Equal(pub.Id, results[0].Id);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static LearningResource MakeResource(
        string userId = UserId,
        string title = "Resource",
        string url = "https://example.com",
        bool isPublic = false,
        bool isArchived = false) =>
        new()
        {
            UserId = userId,
            Title = title,
            Url = url,
            ContentType = ContentType.BlogPost,
            Status = ContentStatus.ToLearn,
            Priority = Priority.Medium,
            IsPublic = isPublic,
            IsArchived = isArchived
        };
}
