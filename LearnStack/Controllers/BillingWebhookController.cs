using LearnStack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnStack.Controllers;

/// <summary>
/// Inbound billing-provider webhook endpoint. All verification and state-application logic
/// lives in <see cref="IBillingWebhookProcessor"/>; this controller only reads the request and
/// maps the result to an HTTP status.
/// </summary>
public class BillingWebhookController(IBillingWebhookProcessor processor) : Controller
{
    /// <summary>The header Stripe signs its webhook payloads with.</summary>
    private const string StripeSignatureHeaderName = "Stripe-Signature";

    [HttpPost("/api/webhooks/billing")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers[StripeSignatureHeaderName].ToString();

        var result = await processor.ProcessAsync(payload, signature, cancellationToken);

        // Every accepted outcome (applied, already-processed, or an event type we don't act on)
        // returns 200 so the provider stops retrying. Only a failed signature check is rejected.
        return result.Accepted ? Ok() : Unauthorized();
    }
}
