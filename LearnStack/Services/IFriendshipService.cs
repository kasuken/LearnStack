using LearnStack.Data.Models;

namespace LearnStack.Services;

public record FriendViewModel(string UserId, string UserName, DateTime ConnectedSince);

public interface IFriendshipService
{
    /// <summary>Creates a new single-use invite link for the given user.</summary>
    Task<FriendInvitation> CreateInvitationAsync(string inviterId);

    /// <summary>Retrieves an invitation by its token without validating state.</summary>
    Task<FriendInvitation?> GetInvitationByTokenAsync(string token);

    /// <summary>
    /// Accepts an invitation. Returns the updated invitation on success,
    /// or null when the token is invalid, expired, already used,
    /// the users are already friends, or the acceptor is the inviter.
    /// </summary>
    Task<FriendInvitation?> AcceptInvitationAsync(string token, string acceptingUserId);

    /// <summary>Returns all friends of the given user with display metadata.</summary>
    Task<List<FriendViewModel>> GetFriendsAsync(string userId);

    /// <summary>Returns true when the two users are friends.</summary>
    Task<bool> IsFriendAsync(string userId, string otherUserId);

    /// <summary>Removes the friendship between the two users.</summary>
    Task RemoveFriendAsync(string userId, string friendUserId);
}
