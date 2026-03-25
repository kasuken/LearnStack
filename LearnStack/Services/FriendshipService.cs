using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class FriendshipService : IFriendshipService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public FriendshipService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<FriendInvitation> CreateInvitationAsync(string inviterId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var invitation = new FriendInvitation
        {
            Token = Guid.NewGuid().ToString("N"),
            InviterId = inviterId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        context.FriendInvitations.Add(invitation);
        await context.SaveChangesAsync();
        return invitation;
    }

    public async Task<FriendInvitation?> GetInvitationByTokenAsync(string token)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FriendInvitations
            .Include(fi => fi.Inviter)
            .FirstOrDefaultAsync(fi => fi.Token == token);
    }

    public async Task<FriendInvitation?> AcceptInvitationAsync(string token, string acceptingUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var invitation = await context.FriendInvitations
            .Include(fi => fi.Inviter)
            .FirstOrDefaultAsync(fi => fi.Token == token);

        if (invitation == null || invitation.IsUsed || invitation.ExpiresAt < DateTime.UtcNow)
            return null;

        // Cannot accept your own invitation
        if (invitation.InviterId == acceptingUserId)
            return null;

        // Skip if already friends
        if (await context.LearnerFriendships.AnyAsync(lf =>
            (lf.RequesterId == invitation.InviterId && lf.AddresseeId == acceptingUserId) ||
            (lf.RequesterId == acceptingUserId && lf.AddresseeId == invitation.InviterId)))
            return null;

        var friendship = new LearnerFriendship
        {
            RequesterId = invitation.InviterId,
            AddresseeId = acceptingUserId,
            DateCreated = DateTime.UtcNow
        };

        invitation.IsUsed = true;
        invitation.AcceptedByUserId = acceptingUserId;
        invitation.AcceptedAt = DateTime.UtcNow;

        context.LearnerFriendships.Add(friendship);
        await context.SaveChangesAsync();
        return invitation;
    }

    public async Task<List<FriendViewModel>> GetFriendsAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var asRequester = await context.LearnerFriendships
            .Where(lf => lf.RequesterId == userId)
            .Include(lf => lf.Addressee)
            .Select(lf => new FriendViewModel(
                lf.AddresseeId,
                lf.Addressee!.UserName ?? lf.AddresseeId,
                lf.DateCreated))
            .ToListAsync();

        var asAddressee = await context.LearnerFriendships
            .Where(lf => lf.AddresseeId == userId)
            .Include(lf => lf.Requester)
            .Select(lf => new FriendViewModel(
                lf.RequesterId,
                lf.Requester!.UserName ?? lf.RequesterId,
                lf.DateCreated))
            .ToListAsync();

        return asRequester
            .Concat(asAddressee)
            .OrderByDescending(f => f.ConnectedSince)
            .ToList();
    }

    public async Task<bool> IsFriendAsync(string userId, string otherUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LearnerFriendships.AnyAsync(lf =>
            (lf.RequesterId == userId && lf.AddresseeId == otherUserId) ||
            (lf.RequesterId == otherUserId && lf.AddresseeId == userId));
    }

    public async Task RemoveFriendAsync(string userId, string friendUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var friendship = await context.LearnerFriendships.FirstOrDefaultAsync(lf =>
            (lf.RequesterId == userId && lf.AddresseeId == friendUserId) ||
            (lf.RequesterId == friendUserId && lf.AddresseeId == userId));

        if (friendship != null)
        {
            context.LearnerFriendships.Remove(friendship);
            await context.SaveChangesAsync();
        }
    }
}
