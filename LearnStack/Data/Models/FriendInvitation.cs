using System.ComponentModel.DataAnnotations;

namespace LearnStack.Data.Models;

public class FriendInvitation
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public string InviterId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public bool IsUsed { get; set; }

    public string? AcceptedByUserId { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public ApplicationUser? Inviter { get; set; }

    public ApplicationUser? AcceptedBy { get; set; }
}
