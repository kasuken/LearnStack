using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LearnStack.Services;

public sealed class DonationService : IDonationService
{
    private static readonly TimeSpan PromptCooldown = TimeSpan.FromDays(1);

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<DonationService> _logger;

    public DonationService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IOptions<StripeOptions> stripeOptions,
        ILogger<DonationService> logger)
    {
        _contextFactory = contextFactory;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        string userId,
        long amountInCents,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var client = new StripeClient(_stripeOptions.SecretKey);

        var successUrl = $"{baseUrl.TrimEnd('/')}/api/donation/success?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl.TrimEnd('/')}/api/donation/cancel";

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = amountInCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "LearnStack Donation",
                            Description = "Thank you for supporting LearnStack! ☕"
                        }
                    },
                    Quantity = 1
                }
            ],
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId
            }
        };

        var service = new SessionService(client);
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Donations.Add(new Donation
        {
            UserId = userId,
            AmountInCents = amountInCents,
            Currency = "usd",
            StripeSessionId = session.Id,
            Status = DonationStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);

        return session.Url;
    }

    public async Task ConfirmDonationAsync(
        string stripeSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var donation = await context.Donations
            .FirstOrDefaultAsync(d => d.StripeSessionId == stripeSessionId, cancellationToken);

        if (donation is null)
        {
            _logger.LogWarning("Donation not found for Stripe session");
            return;
        }

        var client = new StripeClient(_stripeOptions.SecretKey);
        var service = new SessionService(client);
        var session = await service.GetAsync(stripeSessionId, cancellationToken: cancellationToken);

        donation.Status = session.PaymentStatus == "paid" ? DonationStatus.Completed : DonationStatus.Failed;
        donation.StripePaymentIntentId = session.PaymentIntentId;

        if (donation.Status == DonationStatus.Completed)
        {
            var user = await context.Users.FindAsync([donation.UserId], cancellationToken);
            if (user is not null)
            {
                user.HasDonated = true;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordPromptShownAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.Users.FindAsync([userId], cancellationToken);
        if (user is not null)
        {
            user.LastDonationPromptUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ShouldShowPromptAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.Users.FindAsync([userId], cancellationToken);

        if (user is null || user.HasDonated)
            return false;

        if (user.LastDonationPromptUtc.HasValue &&
            DateTime.UtcNow - user.LastDonationPromptUtc.Value < PromptCooldown)
            return false;

        return true;
    }
}
