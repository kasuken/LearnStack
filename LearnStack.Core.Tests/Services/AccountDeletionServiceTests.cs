using LearnStack.Data.Models;
using LearnStack.Services;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Core.Tests.Services;

public class AccountDeletionServiceTests
{
    private const string UserId = "user-1";
    private const string FriendUserId = "user-2";

    private static Task<TestDbContextFactory> MakeFactory() =>
        TestDbContextFactory.CreateAsync(UserId, FriendUserId);

    [Fact]
    public void Constructor_WhenContextFactoryIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AccountDeletionService(null!));
    }

    [Fact]
    public async Task DeleteAccountAsync_WhenUserDoesNotExist_ReturnsFalse()
    {
        await using var factory = await MakeFactory();
        var service = new AccountDeletionService(factory);

        var result = await service.DeleteAccountAsync("no-such-user");

        Assert.False(result);
    }

    /// <summary>
    /// Reproduces the FK conflict described in issue #53: a user who has linked a
    /// learning resource to a content idea, shared a resource in a collection, and
    /// has friends/invitations must be deletable without violating the NoAction
    /// foreign keys on ContentIdeaResource/SharedResourceGroupItem -> LearningResource
    /// and on the "addressee"/"accepted by" side of friendships/invitations.
    /// </summary>
    [Fact]
    public async Task DeleteAccountAsync_WithIdeasSharedCollectionsAndFriends_DeletesEverythingAndLeavesNoOrphans()
    {
        await using var factory = await MakeFactory();

        int resourceId;
        int otherResourceId;
        int ideaId;
        int groupId;

        await using (var context = factory.CreateDbContext())
        {
            var resource = new LearningResource
            {
                Url = "https://example.com/resource",
                Title = "Resource",
                ContentType = ContentType.Article,
                UserId = UserId
            };
            context.LearningResources.Add(resource);

            var otherResource = new LearningResource
            {
                Url = "https://example.com/other-resource",
                Title = "Other Resource",
                ContentType = ContentType.Article,
                UserId = FriendUserId
            };
            context.LearningResources.Add(otherResource);

            await context.SaveChangesAsync();
            resourceId = resource.Id;
            otherResourceId = otherResource.Id;

            var idea = new ContentIdea
            {
                Title = "Idea referencing my resource",
                ContentType = ContentIdeaType.BlogPost,
                UserId = UserId
            };
            context.ContentIdeas.Add(idea);
            await context.SaveChangesAsync();
            ideaId = idea.Id;

            context.ContentIdeaResources.Add(new ContentIdeaResource
            {
                ContentIdeaId = ideaId,
                LearningResourceId = resourceId
            });

            var group = new SharedResourceGroup
            {
                Name = "My shared collection",
                UserId = UserId
            };
            context.SharedResourceGroups.Add(group);
            await context.SaveChangesAsync();
            groupId = group.Id;

            context.SharedResourceGroupItems.Add(new SharedResourceGroupItem
            {
                SharedResourceGroupId = groupId,
                LearningResourceId = resourceId
            });

            // Friendship where the user being deleted is the "Addressee" - the
            // NoAction side of LearnerFriendship.
            context.LearnerFriendships.Add(new LearnerFriendship
            {
                RequesterId = FriendUserId,
                AddresseeId = UserId
            });

            // Invitation created by the other user and accepted by the user being
            // deleted - AcceptedByUserId is the NoAction side of FriendInvitation.
            context.FriendInvitations.Add(new FriendInvitation
            {
                Token = Guid.NewGuid().ToString("N"),
                InviterId = FriendUserId,
                IsUsed = true,
                AcceptedByUserId = UserId,
                AcceptedAt = DateTime.UtcNow
            });

            // Invitation created by the user being deleted, never accepted -
            // InviterId is the Cascade side, included for completeness.
            context.FriendInvitations.Add(new FriendInvitation
            {
                Token = Guid.NewGuid().ToString("N"),
                InviterId = UserId
            });

            await context.SaveChangesAsync();
        }

        var service = new AccountDeletionService(factory);

        var result = await service.DeleteAccountAsync(UserId);

        Assert.True(result);

        await using var verifyContext = factory.CreateDbContext();

        Assert.Null(await verifyContext.Users.FindAsync(UserId));
        Assert.NotNull(await verifyContext.Users.FindAsync(FriendUserId));

        Assert.False(await verifyContext.LearningResources.AnyAsync(lr => lr.UserId == UserId));
        Assert.False(await verifyContext.ContentIdeas.AnyAsync(ci => ci.UserId == UserId));
        Assert.False(await verifyContext.SharedResourceGroups.AnyAsync(sg => sg.UserId == UserId));
        Assert.False(await verifyContext.ContentIdeaResources.AnyAsync(cir =>
            cir.ContentIdeaId == ideaId || cir.LearningResourceId == resourceId));
        Assert.False(await verifyContext.SharedResourceGroupItems.AnyAsync(sgi =>
            sgi.SharedResourceGroupId == groupId || sgi.LearningResourceId == resourceId));
        Assert.False(await verifyContext.LearnerFriendships.AnyAsync(lf =>
            lf.RequesterId == UserId || lf.AddresseeId == UserId));
        Assert.False(await verifyContext.FriendInvitations.AnyAsync(fi =>
            fi.InviterId == UserId || fi.AcceptedByUserId == UserId));

        // The other user's own resource is untouched.
        Assert.NotNull(await verifyContext.LearningResources.FindAsync(otherResourceId));
    }
}
