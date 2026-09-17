using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace LearnStack.Services;

/// <summary>
/// Default <see cref="ISmtpClient"/> implementation backed by <see cref="System.Net.Mail.SmtpClient"/>,
/// configured entirely from <see cref="EmailSenderOptions"/> (bound from appsettings/IConfiguration).
/// A new <see cref="System.Net.Mail.SmtpClient"/> is created per send, matching Microsoft's guidance
/// that instances should not be reused across concurrent sends.
/// </summary>
public class SmtpClientWrapper : ISmtpClient
{
    private readonly EmailSenderOptions _options;

    public SmtpClientWrapper(IOptions<EmailSenderOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendMailAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
