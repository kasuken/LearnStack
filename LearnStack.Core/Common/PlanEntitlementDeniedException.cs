namespace LearnStack.Common;

/// <summary>
/// Thrown when a server-side entitlement check blocks a paid-plan-gated action (currently, the
/// Starter resource cap). Carries a user-facing explanation so callers can surface a clear
/// message instead of a generic error.
/// </summary>
public sealed class PlanEntitlementDeniedException(string message) : InvalidOperationException(message);
