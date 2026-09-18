using LearnStack.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

/// <summary>
/// Deletes a user's account together with every dependent row that would
/// otherwise violate a foreign key constraint. Several relationships
/// (<c>ContentIdeaResource</c> and <c>SharedResourceGroupItem</c> towards
/// <c>LearningResource</c>, and the "addressee"/"accepted by" side of
/// friendships and invitations) are configured with
/// <see cref="DeleteBehavior.NoAction"/>, so relying on
/// <c>UserManager.DeleteAsync</c>'s cascading behavior alone fails with a
/// foreign key conflict for any user who has linked a resource to a content
/// idea, added one to a shared collection, or accepted/received a friend
/// request or invitation.
/// </summary>
public class AccountDeletionService(IDbContextFactory<ApplicationDbContext> contextFactory) : IAccountDeletionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<bool> DeleteAccountAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return false;

        await using var transaction = await context.Database.BeginTransactionAsync();

        var resourceIds = await context.LearningResources
            .Where(lr => lr.UserId == userId)
            .Select(lr => lr.Id)
            .ToListAsync();

        var ideaIds = await context.ContentIdeas
            .Where(ci => ci.UserId == userId)
            .Select(ci => ci.Id)
            .ToListAsync();

        var groupIds = await context.SharedResourceGroups
            .Where(sg => sg.UserId == userId)
            .Select(sg => sg.Id)
            .ToListAsync();

        // 1. Idea <-> resource links. Removed first because LearningResourceId
        //    uses DeleteBehavior.NoAction and would block resource deletion.
        var ideaResourceLinks = await context.ContentIdeaResources
            .Where(cir => ideaIds.Contains(cir.ContentIdeaId) || resourceIds.Contains(cir.LearningResourceId))
            .ToListAsync();
        context.ContentIdeaResources.RemoveRange(ideaResourceLinks);

        // 2. Shared-collection items. Same reasoning for LearningResourceId.
        var sharedItems = await context.SharedResourceGroupItems
            .Where(sgi => groupIds.Contains(sgi.SharedResourceGroupId) || resourceIds.Contains(sgi.LearningResourceId))
            .ToListAsync();
        context.SharedResourceGroupItems.RemoveRange(sharedItems);

        await context.SaveChangesAsync();

        // 3. Shared collection groups, 4. content ideas, 5. learning resources
        //    owned by the user. No remaining rows reference any of these.
        var groups = await context.SharedResourceGroups.Where(sg => sg.UserId == userId).ToListAsync();
        context.SharedResourceGroups.RemoveRange(groups);

        var ideas = await context.ContentIdeas.Where(ci => ci.UserId == userId).ToListAsync();
        context.ContentIdeas.RemoveRange(ideas);

        var resources = await context.LearningResources.Where(lr => lr.UserId == userId).ToListAsync();
        context.LearningResources.RemoveRange(resources);

        await context.SaveChangesAsync();

        // 6. Friend invitations sent by, or accepted by, the user.
        //    AcceptedByUserId uses DeleteBehavior.NoAction.
        var invitations = await context.FriendInvitations
            .Where(fi => fi.InviterId == userId || fi.AcceptedByUserId == userId)
            .ToListAsync();
        context.FriendInvitations.RemoveRange(invitations);

        // 7. Friendships in either direction. AddresseeId uses DeleteBehavior.NoAction.
        var friendships = await context.LearnerFriendships
            .Where(lf => lf.RequesterId == userId || lf.AddresseeId == userId)
            .ToListAsync();
        context.LearnerFriendships.RemoveRange(friendships);

        await context.SaveChangesAsync();

        // 8. Finally, the user account itself. Identity's own dependents
        //    (claims, logins, tokens, roles) remain cascade-configured.
        context.Users.Remove(user);
        await context.SaveChangesAsync();

        await transaction.CommitAsync();
        return true;
    }
}
