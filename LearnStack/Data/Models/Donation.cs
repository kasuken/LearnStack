namespace LearnStack.Data.Models;

public class Donation
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long AmountInCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string StripeSessionId { get; set; } = string.Empty;
    public string? StripePaymentIntentId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "pending";

    public ApplicationUser User { get; set; } = null!;
}
