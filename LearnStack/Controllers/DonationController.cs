using LearnStack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace LearnStack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DonationController : ControllerBase
{
    private readonly IDonationService _donationService;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<DonationController> _logger;

    public DonationController(
        IDonationService donationService,
        IOptions<StripeOptions> stripeOptions,
        ILogger<DonationController> logger)
    {
        _donationService = donationService;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    /// <summary>Stripe webhook endpoint — verifies the signature and handles checkout.session.completed.</summary>
    [HttpPost("webhook")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(cancellationToken);

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _stripeOptions.WebhookSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session is not null)
                {
                    await _donationService.ConfirmDonationAsync(session.Id, cancellationToken);
                }
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest("Invalid Stripe signature");
        }
    }

    /// <summary>Stripe redirects here after a successful checkout — marks donation confirmed.</summary>
    [HttpGet("success")]
    public async Task<IActionResult> Success(
        [FromQuery(Name = "session_id")] string sessionId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await _donationService.ConfirmDonationAsync(sessionId, cancellationToken);
        }

        return Redirect("/donation/thank-you");
    }

    /// <summary>Stripe redirects here when the user cancels the checkout.</summary>
    [HttpGet("cancel")]
    public IActionResult Cancel() => Redirect("/resources");
}
