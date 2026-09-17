using LearnStack.Data;
using LearnStack.Data.Models;
using LearnStack.Services;

namespace LearnStack.Core.Tests.Services;

public class FriendshipServiceTests
{
    private const string UserId = "user-1";
    private const string FriendId = "user-2";

    private static Task<TestDbContextFactory> MakeFactory() =>
        TestDbContextFactory.CreateAsync(UserId, FriendId);

    [Fact]
    public async Task GetFriendsAsync_WhenFriendHasDisplayName_ReturnsDisplayNameNotEmail()
    {
        await using var factory = await MakeFactory();
        await SetDisplayNameAsync(factory, FriendId, "Jane Doe");
        await AddFriendshipAsync(factory, UserId, FriendId);

        var service = new FriendshipService(factory);
        var friends = await service.GetFriendsAsync(UserId);

        var friend = Assert.Single(friends);
        Assert.Equal("Jane Doe", friend.DisplayName);
        Assert.DoesNotContain('@', friend.DisplayName);
    }

    [Fact]
    public async Task GetFriendsAsync_WhenFriendHasNoDisplayName_FallsBackToEmailLocalPartOnly()
    {
        await using var factory = await MakeFactory();
        // No DisplayName set; the seeded user has Email "user-2@test.local".
        await AddFriendshipAsync(factory, UserId, FriendId);

        var service = new FriendshipService(factory);
        var friends = await service.GetFriendsAsync(UserId);

        var friend = Assert.Single(friends);
        Assert.Equal("user-2", friend.DisplayName);
        Assert.DoesNotContain('@', friend.DisplayName);
    }

    [Fact]
    public async Task GetFriendsAsync_ReturnsFriendRegardlessOfWhoInitiatedTheFriendship()
    {
        await using var factory = await MakeFactory();
        await SetDisplayNameAsync(factory, UserId, "Requester Name");
        await AddFriendshipAsync(factory, FriendId, UserId); // FriendId is the requester

        var service = new FriendshipService(factory);
        var friends = await service.GetFriendsAsync(UserId);

        var friend = Assert.Single(friends);
        Assert.Equal(FriendId, friend.UserId);
    }

    private static async Task SetDisplayNameAsync(TestDbContextFactory factory, string userId, string displayName)
    {
        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Users.FindAsync(userId);
        Assert.NotNull(user);
        user!.DisplayName = displayName;
        await context.SaveChangesAsync();
    }

    private static async Task AddFriendshipAsync(TestDbContextFactory factory, string requesterId, string addresseeId)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.LearnerFriendships.Add(new LearnerFriendship
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            DateCreated = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}
