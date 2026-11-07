using System.Text.Json;
using LearnStack.Data.Models;
using LearnStack.Services;

namespace LearnStack.Core.Tests.Services;

public class PersonalDataExportServiceTests
{
    private const string UserId = "user-1";
    private const string OtherId = "user-2";

    [Fact]
    public async Task ExportAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId);
        var service = new PersonalDataExportService(factory);

        Assert.Null(await service.ExportAsync("nobody"));
    }

    [Fact]
    public async Task ExportAsync_IncludesOnlyTheRequestedUsersContent()
    {
        await using var factory = await TestDbContextFactory.CreateAsync(UserId, OtherId);
        await using (var context = await factory.CreateDbContextAsync())
        {
            var mine = new LearningResource { Url = "https://example.com/a", Title = "Mine", Notes = "my note", ContentType = ContentType.Article, UserId = UserId };
            var theirs = new LearningResource { Url = "https://example.com/b", Title = "Theirs", ContentType = ContentType.Article, UserId = OtherId };
            context.LearningResources.AddRange(mine, theirs);
            context.ContentIdeas.Add(new ContentIdea { Title = "Idea", ContentType = ContentIdeaType.BlogPost, UserId = UserId });
            context.ContentIdeas.Add(new ContentIdea { Title = "Their idea", ContentType = ContentIdeaType.BlogPost, UserId = OtherId });
            context.SharedResourceGroups.Add(new SharedResourceGroup { Name = "Group", UserId = UserId });
            context.LearnerFriendships.Add(new LearnerFriendship { RequesterId = OtherId, AddresseeId = UserId });
            await context.SaveChangesAsync();
        }

        var export = await new PersonalDataExportService(factory).ExportAsync(UserId);

        Assert.NotNull(export);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(export));
        var root = json.RootElement;

        var resources = root.GetProperty("LearningResources");
        Assert.Equal(1, resources.GetArrayLength());
        Assert.Equal("Mine", resources[0].GetProperty("Title").GetString());
        Assert.Equal("my note", resources[0].GetProperty("Notes").GetString());
        Assert.Equal(1, root.GetProperty("ContentIdeas").GetArrayLength());
        Assert.Equal("Group", root.GetProperty("SharedCollections")[0].GetProperty("Name").GetString());

        var friendship = root.GetProperty("Friendships")[0];
        Assert.Equal("received", friendship.GetProperty("Direction").GetString());
        Assert.Equal(OtherId, friendship.GetProperty("OtherUserId").GetString());
    }
}
