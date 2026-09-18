using System.Net.Mail;

namespace LearnStack.Services;

/// <summary>
/// Thin abstraction over the SMTP transport used to deliver mail, so email-sending
/// logic can be unit tested without opening a real network connection.
/// </summary>
public interface ISmtpClient
{
    Task SendMailAsync(MailMessage message, CancellationToken cancellationToken = default);
}
