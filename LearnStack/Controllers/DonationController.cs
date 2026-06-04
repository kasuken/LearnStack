using LearnStack.Data;
using LearnStack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LearnStack.Controllers;

[Authorize]
[Route("donation")]
public class DonationController(
    UserManager<ApplicationUser> userManager,
    StripeDonationService stripeDonationService) : Controller
{
    /// <summary>
    /// Starts the donation checkout flow and redirects the user to Stripe.
    /// </summary>
    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        if (!stripeDonationService.IsConfigured)
        {
            return Redirect("/resources");
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (user.DonatedAtUtc.HasValue)
        {
            return Redirect("/resources");
        }

        var checkoutUrl = await stripeDonationService.CreateCheckoutUrlAsync(
            user.Id,
            $"{Request.Scheme}://{Request.Host}",
            cancellationToken);

        return string.IsNullOrWhiteSpace(checkoutUrl) ? Redirect("/resources") : Redirect(checkoutUrl);
    }

    /// <summary>
    /// Confirms a Stripe checkout session and stores the donation flag for the current user.
    /// </summary>
    [HttpGet("complete")]
    public async Task<IActionResult> Complete([FromQuery(Name = "session_id")] string? sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Redirect("/resources");
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var donationConfirmed = await stripeDonationService.ConfirmDonationAsync(user.Id, sessionId, cancellationToken);
        if (donationConfirmed && !user.DonatedAtUtc.HasValue)
        {
            user.DonatedAtUtc = DateTime.UtcNow;
            user.LastDonationPromptAtUtc = null;
            await userManager.UpdateAsync(user);
        }

        return Redirect("/resources");
    }
}
