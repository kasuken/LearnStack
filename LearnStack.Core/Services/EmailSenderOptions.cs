namespace LearnStack.Services;

/// <summary>
/// SMTP configuration for outgoing account emails (confirmation, password reset, email change).
/// Bound from the "Email" section of appsettings.json / user secrets / environment variables.
/// Any SMTP-compatible provider (SendGrid, Azure Communication Services, Resend, etc.) can be
/// used by pointing these settings at that provider's SMTP relay - no code changes required.
/// </summary>
public class EmailSenderOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP server host name, e.g. "smtp.sendgrid.net".</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP server port, e.g. 587 for STARTTLS.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Whether to use SSL/TLS when connecting to the SMTP server.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Username used to authenticate with the SMTP server, if required.</summary>
    public string? UserName { get; set; }

    /// <summary>Password used to authenticate with the SMTP server, if required.</summary>
    public string? Password { get; set; }

    /// <summary>Email address that outgoing messages are sent from.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Display name that outgoing messages are sent from.</summary>
    public string FromName { get; set; } = "LearnStack";
}
