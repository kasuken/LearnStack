using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class FriendshipService : IFriendshipService
{
    private readonly ApplicationDbContext _context;

    public FriendshipService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FriendInvitation> CreateInvitationAsync(string inviterId)
    {
        var invitation = new FriendInvitation
        {
            Token = Guid.NewGuid().ToString("N"),
            InviterId = inviterId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _context.FriendInvitations.Add(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task<FriendInvitation?> GetInvitationByTokenAsync(string token)
    {
        return await _context.FriendInvitations
            .Include(fi => fi.Inviter)
            .FirstOrDefaultAsync(fi => fi.Token == token);
    }

    public async Task<FriendInvitation?> AcceptInvitationAsync(string token, string acceptingUserId)
    {
        var invitation = await _context.FriendInvitations
            .Include(fi => fi.Inviter)
            .FirstOrDefaultAsync(fi => fi.Token == token);

        if (invitation == null || invitation.IsUsed || invitation.ExpiresAt < DateTime.UtcNow)
            return null;

        // Cannot accept your own invitation
        if (invitation.InviterId == acceptingUserId)
            return null;

        // Skip if already friends
        if (await IsFriendAsync(invitation.InviterId, acceptingUserId))
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

        _context.LearnerFriendships.Add(friendship);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task<List<FriendViewModel>> GetFriendsAsync(string userId)
    {
        var asRequester = await _context.LearnerFriendships
            .Where(lf => lf.RequesterId == userId)
            .Include(lf => lf.Addressee)
            .Select(lf => new FriendViewModel(
                lf.AddresseeId,
                lf.Addressee!.UserName ?? lf.AddresseeId,
                lf.DateCreated))
            .ToListAsync();

        var asAddressee = await _context.LearnerFriendships
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
        return await _context.LearnerFriendships.AnyAsync(lf =>
            (lf.RequesterId == userId && lf.AddresseeId == otherUserId) ||
            (lf.RequesterId == otherUserId && lf.AddresseeId == userId));
    }

    public async Task RemoveFriendAsync(string userId, string friendUserId)
    {
        var friendship = await _context.LearnerFriendships.FirstOrDefaultAsync(lf =>
            (lf.RequesterId == userId && lf.AddresseeId == friendUserId) ||
            (lf.RequesterId == friendUserId && lf.AddresseeId == userId));

        if (friendship != null)
        {
            _context.LearnerFriendships.Remove(friendship);
            await _context.SaveChangesAsync();
        }
    }
}
