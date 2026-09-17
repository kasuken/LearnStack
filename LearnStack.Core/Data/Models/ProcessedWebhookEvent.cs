using System.ComponentModel.DataAnnotations;

namespace LearnStack.Data.Models;

/// <summary>
/// Idempotency ledger for inbound billing webhooks: records that a provider's event id has
/// already been applied, so at-least-once webhook delivery (retries) becomes a safe no-op
/// instead of re-applying a plan change. Also serves as an auditable record of billing events
/// received.
/// </summary>
public class ProcessedWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The billing provider's unique event id (e.g. Stripe's <c>evt_...</c> id).</summary>
    [Required]
    [MaxLength(255)]
    public string ProviderEventId { get; set; } = string.Empty;

    /// <summary>The provider's event type string (e.g. <c>customer.subscription.updated</c>), for audit.</summary>
    [MaxLength(100)]
    public string? EventType { get; set; }

    /// <summary>The LearnStack user the event applied a plan change to, when known.</summary>
    public string? TargetUserId { get; set; }

    /// <summary>When this event was first (and only) applied.</summary>
    public DateTime ProcessedAtUtc { get; set; }
}
