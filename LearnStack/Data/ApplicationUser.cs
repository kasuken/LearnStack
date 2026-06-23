using Microsoft.AspNetCore.Identity;

namespace LearnStack.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public DateTime? TosAcceptedAt { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }
    public bool HasDonated { get; set; }
    public DateTime? LastDonationPromptUtc { get; set; }
}