namespace LearnStack.Services;

public interface IDonationService
{
    /// <summary>Creates a Stripe Checkout session and returns the redirect URL.</summary>
    Task<string> CreateCheckoutSessionAsync(string userId, long amountInCents, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>Marks the user as having donated and records the completed donation.</summary>
    Task ConfirmDonationAsync(string stripeSessionId, CancellationToken cancellationToken = default);

    /// <summary>Records that the donation prompt was shown to the user.</summary>
    Task RecordPromptShownAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Returns true when the prompt should be shown for the given user.</summary>
    Task<bool> ShouldShowPromptAsync(string userId, CancellationToken cancellationToken = default);
}
