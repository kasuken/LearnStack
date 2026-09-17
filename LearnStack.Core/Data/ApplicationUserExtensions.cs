namespace LearnStack.Data;

public static class ApplicationUserExtensions
{
    /// <summary>
    /// Returns the user's chosen display name, falling back to the local part
    /// of their email/username when no display name has been set. This keeps
    /// the full email address from ever being surfaced beyond this fallback.
    /// </summary>
    public static string GetDisplayName(this ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            return user.DisplayName;

        var userName = user.UserName;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var atIndex = userName.IndexOf('@');
            return atIndex > 0 ? userName[..atIndex] : userName;
        }

        return user.Id;
    }
}
