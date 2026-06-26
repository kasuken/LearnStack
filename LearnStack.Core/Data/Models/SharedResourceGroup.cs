using System.ComponentModel.DataAnnotations;

namespace LearnStack.Data.Models;

public class SharedResourceGroup
{
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(64)]
    public string ShareToken { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsActive { get; set; } = true;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public ICollection<SharedResourceGroupItem> Items { get; set; } = new List<SharedResourceGroupItem>();
}
