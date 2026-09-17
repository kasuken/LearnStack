using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace LearnStack.Data;

public class ApplicationUser : IdentityUser
{
    public DateTime? LastAccessAt { get; set; }
    public DateTime? TosAcceptedAt { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }

    [MaxLength(100)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// The user's IANA time zone identifier (e.g. "America/Los_Angeles").
    /// Used to convert stored UTC timestamps to the user's local time for
    /// display and analytics. Null until detected or set by the user.
    /// </summary>
    [MaxLength(100)]
    public string? TimeZoneId { get; set; }
}
