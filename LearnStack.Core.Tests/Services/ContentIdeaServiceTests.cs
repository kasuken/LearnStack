using LearnStack.Data.Models;
using LearnStack.Services;

namespace LearnStack.Core.Tests.Services;

public class ContentIdeaServiceTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private static Task<TestDbContextFactory> MakeFactory() =>
        TestDbContextFactory.CreateAsync(UserId, OtherUserId);

    [Fact]
    public void Constructor_WhenContextFactoryIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ContentIdeaService(null!));
    }

    [Fact]
    public async Task CreateAsync_WhenIdeaIsNull_ThrowsArgumentNullException()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_StoresAndReturnsIdea()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);

        var idea = await service.CreateAsync(MakeIdea(UserId, "Test idea"));

        Assert.True(idea.Id > 0);

        var fetched = await service.GetByIdAsync(idea.Id, UserId);
        Assert.NotNull(fetched);
        Assert.Equal("Test idea", fetched.Title);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyIdeasForOwnerInExpectedOrder()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var olderHighPriorityIdea = MakeIdea(UserId, "Older high priority");
        olderHighPriorityIdea.Priority = Priority.High;
        olderHighPriorityIdea.CustomOrder = 1;
        olderHighPriorityIdea.DateCreated = DateTime.UtcNow.AddDays(-1);
        var newerHighPriorityIdea = MakeIdea(UserId, "Newer high priority");
        newerHighPriorityIdea.Priority = Priority.High;
        newerHighPriorityIdea.CustomOrder = 1;

        await service.CreateAsync(MakeIdea(UserId, "Medium priority"));
        await service.CreateAsync(olderHighPriorityIdea);
        await service.CreateAsync(newerHighPriorityIdea);
        await service.CreateAsync(MakeIdea(OtherUserId, "Other user's idea"));

        var results = await service.GetAllAsync(UserId);

        Assert.Equal(
            ["Newer high priority", "Older high priority", "Medium priority"],
            results.Select(idea => idea.Title));
    }

    [Fact]
    public async Task GetByIdAsync_WhenIdeaBelongsToAnotherUser_ReturnsNull()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(OtherUserId));

        var result = await service.GetByIdAsync(idea.Id, UserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEditableFieldsWithoutChangingOwnershipOrCreationDate()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var original = MakeIdea(UserId);
        original.DateCreated = DateTime.UtcNow.AddDays(-2);
        await service.CreateAsync(original);
        var update = MakeIdea(OtherUserId, "Updated idea");
        update.Id = original.Id;
        update.ContentType = ContentIdeaType.Video;
        update.Description = "Updated description";
        update.Outline = "Updated outline";
        update.Status = IdeaStatus.Published;
        update.Priority = Priority.High;
        update.Notes = "Updated notes";
        update.DatePublished = DateTime.UtcNow;
        update.CustomOrder = 4;

        var result = await service.UpdateAsync(update, UserId);

        Assert.NotNull(result);
        Assert.Equal("Updated idea", result.Title);
        Assert.Equal(ContentIdeaType.Video, result.ContentType);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal("Updated outline", result.Outline);
        Assert.Equal(IdeaStatus.Published, result.Status);
        Assert.Equal(Priority.High, result.Priority);
        Assert.Equal("Updated notes", result.Notes);
        Assert.Equal(update.DatePublished, result.DatePublished);
        Assert.Equal(4, result.CustomOrder);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(original.DateCreated, result.DateCreated);
    }

    [Fact]
    public async Task UpdateAsync_WhenIdeaBelongsToAnotherUser_ReturnsNullAndLeavesIdeaUnchanged()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(OtherUserId, "Original"));
        var update = MakeIdea(UserId, "Changed");
        update.Id = idea.Id;

        var result = await service.UpdateAsync(update, UserId);

        Assert.Null(result);
        var stored = await service.GetByIdAsync(idea.Id, OtherUserId);
        Assert.NotNull(stored);
        Assert.Equal("Original", stored.Title);
    }

    [Fact]
    public async Task UpdateAsync_WhenIdeaIsNull_ThrowsArgumentNullException()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateAsync(null!, UserId));
    }

    [Fact]
    public async Task DeleteAsync_RemovesOwnedIdea()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(UserId));

        var deleted = await service.DeleteAsync(idea.Id, UserId);

        Assert.True(deleted);
        Assert.Null(await service.GetByIdAsync(idea.Id, UserId));
    }

    [Fact]
    public async Task DeleteAsync_WhenIdeaBelongsToAnotherUser_ReturnsFalseAndLeavesIdea()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(OtherUserId));

        var deleted = await service.DeleteAsync(idea.Id, UserId);

        Assert.False(deleted);
        Assert.NotNull(await service.GetByIdAsync(idea.Id, OtherUserId));
    }

    [Fact]
    public async Task AddSourceResourceAsync_AddsLinkForOwnedEntities()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(UserId));
        var resource = await CreateResourceAsync(factory, MakeResource(UserId));

        await service.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

        var fetched = await service.GetByIdAsync(idea.Id, UserId);
        Assert.NotNull(fetched);
        var link = Assert.Single(fetched.SourceResources);
        Assert.Equal(resource.Id, link.LearningResourceId);
        Assert.Equal(resource.Title, link.LearningResource?.Title);
    }

    [Fact]
    public async Task AddSourceResourceAsync_DoesNotDuplicateExistingLink()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(UserId));
        var resource = await CreateResourceAsync(factory, MakeResource(UserId));

        await service.AddSourceResourceAsync(idea.Id, resource.Id, UserId);
        await service.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

        var fetched = await service.GetByIdAsync(idea.Id, UserId);
        Assert.NotNull(fetched);
        Assert.Single(fetched.SourceResources);
    }

    [Fact]
    public async Task AddSourceResourceAsync_WhenIdeaBelongsToAnotherUser_DoesNotAddLink()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(OtherUserId));
        var resource = await CreateResourceAsync(factory, MakeResource(UserId));

        await service.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

        var fetched = await service.GetByIdAsync(idea.Id, OtherUserId);
        Assert.NotNull(fetched);
        Assert.Empty(fetched.SourceResources);
    }

    [Fact]
    public async Task AddSourceResourceAsync_WhenResourceBelongsToAnotherUser_DoesNotAddLink()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(UserId));
        var resource = await CreateResourceAsync(factory, MakeResource(OtherUserId));

        await service.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

        var fetched = await service.GetByIdAsync(idea.Id, UserId);
        Assert.NotNull(fetched);
        Assert.Empty(fetched.SourceResources);
    }

    [Fact]
    public async Task RemoveSourceResourceAsync_RemovesOwnedLink()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(UserId));
        var resource = await CreateResourceAsync(factory, MakeResource(UserId));
        await service.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

        await service.RemoveSourceResourceAsync(idea.Id, resource.Id, UserId);

        var fetched = await service.GetByIdAsync(idea.Id, UserId);
        Assert.NotNull(fetched);
        Assert.Empty(fetched.SourceResources);
    }

    [Fact]
    public async Task RemoveSourceResourceAsync_WhenIdeaBelongsToAnotherUser_DoesNotRemoveLink()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(OtherUserId));
        var resource = await CreateResourceAsync(factory, MakeResource(UserId));
        await CreateLinkAsync(factory, idea.Id, resource.Id);

        await service.RemoveSourceResourceAsync(idea.Id, resource.Id, UserId);

        var fetched = await service.GetByIdAsync(idea.Id, OtherUserId);
        Assert.NotNull(fetched);
        Assert.Single(fetched.SourceResources);
    }

    [Fact]
    public async Task RemoveSourceResourceAsync_WhenResourceBelongsToAnotherUser_DoesNotRemoveLink()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var idea = await service.CreateAsync(MakeIdea(UserId));
        var resource = await CreateResourceAsync(factory, MakeResource(OtherUserId));
        await CreateLinkAsync(factory, idea.Id, resource.Id);

        await service.RemoveSourceResourceAsync(idea.Id, resource.Id, UserId);

        var fetched = await service.GetByIdAsync(idea.Id, UserId);
        Assert.NotNull(fetched);
        Assert.Single(fetched.SourceResources);
    }

    [Fact]
    public async Task UpdateOrderAsync_UpdatesOnlyOwnedIdeas()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);
        var first = await service.CreateAsync(MakeIdea(UserId, "First"));
        var second = await service.CreateAsync(MakeIdea(UserId, "Second"));
        var other = await service.CreateAsync(MakeIdea(OtherUserId, "Other"));

        await service.UpdateOrderAsync(UserId, [second.Id, other.Id, first.Id]);

        var ownedIdeas = await service.GetAllAsync(UserId);
        Assert.Equal(0, ownedIdeas.Single(idea => idea.Id == second.Id).CustomOrder);
        Assert.Equal(2, ownedIdeas.Single(idea => idea.Id == first.Id).CustomOrder);

        var otherIdea = await service.GetByIdAsync(other.Id, OtherUserId);
        Assert.NotNull(otherIdea);
        Assert.Equal(0, otherIdea.CustomOrder);
    }

    [Fact]
    public async Task UpdateOrderAsync_WhenOrderedIdsIsNull_ThrowsArgumentNullException()
    {
        await using var factory = await MakeFactory();
        var service = new ContentIdeaService(factory);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateOrderAsync(UserId, null!));
    }

    private static async Task<LearningResource> CreateResourceAsync(
        TestDbContextFactory factory,
        LearningResource resource)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.LearningResources.Add(resource);
        await context.SaveChangesAsync();
        return resource;
    }

    private static async Task CreateLinkAsync(TestDbContextFactory factory, int ideaId, int resourceId)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.ContentIdeaResources.Add(new ContentIdeaResource
        {
            ContentIdeaId = ideaId,
            LearningResourceId = resourceId
        });
        await context.SaveChangesAsync();
    }

    private static ContentIdea MakeIdea(string userId, string title = "Idea") =>
        new()
        {
            UserId = userId,
            Title = title,
            ContentType = ContentIdeaType.BlogPost,
            Status = IdeaStatus.Idea,
            Priority = Priority.Medium
        };

    private static LearningResource MakeResource(string userId, string title = "Resource") =>
        new()
        {
            UserId = userId,
            Title = title,
            Url = $"https://example.com/{Guid.NewGuid():N}",
            ContentType = ContentType.BlogPost,
            Status = ContentStatus.ToLearn,
            Priority = Priority.Medium
        };
}
