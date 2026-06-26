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
}
