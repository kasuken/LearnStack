using System.Net.Mail;
using LearnStack.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LearnStack.Services;

/// <summary>
/// Production <see cref="IEmailSender{TUser}"/> implementation used by ASP.NET Core Identity to
/// deliver account confirmation, password reset and email-change confirmation messages over SMTP.
/// Replaces the previous no-op sender so those links are actually delivered to users.
/// </summary>
public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly ISmtpClient _smtpClient;
    private readonly EmailSenderOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(ISmtpClient smtpClient, IOptions<EmailSenderOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _smtpClient = smtpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendEmailAsync(email, "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendEmailAsync(email, "Reset your password",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendEmailAsync(email, "Reset your password",
            $"Please reset your password using the following code: {resetCode}");

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        try
        {
            await _smtpClient.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} with subject {Subject}", toEmail, subject);
            throw;
        }
    }
}
