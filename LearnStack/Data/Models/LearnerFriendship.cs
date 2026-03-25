using System.ComponentModel.DataAnnotations;

namespace LearnStack.Data.Models;

public class LearnerFriendship
{
    public int Id { get; set; }

    [Required]
    public string RequesterId { get; set; } = string.Empty;

    [Required]
    public string AddresseeId { get; set; } = string.Empty;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public ApplicationUser? Requester { get; set; }

    public ApplicationUser? Addressee { get; set; }
}
