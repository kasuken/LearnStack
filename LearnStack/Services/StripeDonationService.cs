using LearnStack.Options;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LearnStack.Services;

public class StripeDonationService(IOptions<StripeOptions> stripeOptions)
{
    private readonly StripeOptions _stripeOptions = stripeOptions.Value;

    public bool IsConfigured => _stripeOptions.IsConfigured;

    /// <summary>
    /// Creates a Stripe checkout session URL for a one-time donation.
    /// </summary>
    public async Task<string?> CreateCheckoutUrlAsync(string userId, string baseUrl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        if (!IsConfigured)
        {
            return null;
        }

        StripeConfiguration.ApiKey = _stripeOptions.SecretKey;

        var normalizedBaseUrl = baseUrl.TrimEnd('/');
        var sessionService = new SessionService();
        var checkoutSession = await sessionService.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = _stripeOptions.DonationPriceId,
                        Quantity = 1
                    }
                ],
                SuccessUrl = $"{normalizedBaseUrl}/donation/complete?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{normalizedBaseUrl}/resources",
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId
                }
            },
            cancellationToken: cancellationToken);

        return checkoutSession.Url;
    }

    /// <summary>
    /// Verifies that a checkout session is paid and belongs to the requesting user.
    /// </summary>
    public async Task<bool> ConfirmDonationAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!IsConfigured)
        {
            return false;
        }

        StripeConfiguration.ApiKey = _stripeOptions.SecretKey;

        var sessionService = new SessionService();
        var checkoutSession = await sessionService.GetAsync(sessionId, cancellationToken: cancellationToken);
        if (!string.Equals(checkoutSession.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return checkoutSession.Metadata.TryGetValue("userId", out var checkoutUserId) &&
               string.Equals(checkoutUserId, userId, StringComparison.Ordinal);
    }
}
