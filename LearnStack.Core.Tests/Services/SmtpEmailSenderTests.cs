using System.Net.Mail;
using LearnStack.Data;
using LearnStack.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LearnStack.Core.Tests.Services;

public class SmtpEmailSenderTests
{
    private static readonly EmailSenderOptions TestOptions = new()
    {
        Host = "smtp.example.com",
        Port = 587,
        EnableSsl = true,
        UserName = "smtp-user",
        Password = "smtp-password",
        FromAddress = "noreply@learnstack.dev",
        FromName = "LearnStack"
    };

    private static SmtpEmailSender BuildSender(Mock<ISmtpClient> smtpClientMock) =>
        new(smtpClientMock.Object, Options.Create(TestOptions), NullLogger<SmtpEmailSender>.Instance);

    private static ApplicationUser MakeUser() => new() { UserName = "someone@example.com" };

    [Fact]
    public async Task SendConfirmationLinkAsync_SendsEmailWithRecipientSubjectAndLink()
    {
        var smtpClientMock = new Mock<ISmtpClient>();
        MailMessage? sentMessage = null;
        smtpClientMock
            .Setup(c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, CancellationToken>((message, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        var sender = BuildSender(smtpClientMock);
        var user = MakeUser();

        await sender.SendConfirmationLinkAsync(user, "recipient@example.com", "https://learnstack.dev/confirm?code=abc");

        smtpClientMock.Verify(
            c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(sentMessage);
        Assert.Equal("recipient@example.com", sentMessage!.To.Single().Address);
        Assert.Equal(TestOptions.FromAddress, sentMessage.From!.Address);
        Assert.Equal("Confirm your email", sentMessage.Subject);
        Assert.Contains("https://learnstack.dev/confirm?code=abc", sentMessage.Body);
        Assert.True(sentMessage.IsBodyHtml);
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_SendsEmailWithRecipientSubjectAndLink()
    {
        var smtpClientMock = new Mock<ISmtpClient>();
        MailMessage? sentMessage = null;
        smtpClientMock
            .Setup(c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, CancellationToken>((message, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        var sender = BuildSender(smtpClientMock);
        var user = MakeUser();

        await sender.SendPasswordResetLinkAsync(user, "recipient@example.com", "https://learnstack.dev/reset?code=xyz");

        smtpClientMock.Verify(
            c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(sentMessage);
        Assert.Equal("recipient@example.com", sentMessage!.To.Single().Address);
        Assert.Equal("Reset your password", sentMessage.Subject);
        Assert.Contains("https://learnstack.dev/reset?code=xyz", sentMessage.Body);
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_SendsEmailWithRecipientSubjectAndCode()
    {
        var smtpClientMock = new Mock<ISmtpClient>();
        MailMessage? sentMessage = null;
        smtpClientMock
            .Setup(c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, CancellationToken>((message, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        var sender = BuildSender(smtpClientMock);
        var user = MakeUser();

        await sender.SendPasswordResetCodeAsync(user, "recipient@example.com", "123456");

        smtpClientMock.Verify(
            c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(sentMessage);
        Assert.Equal("recipient@example.com", sentMessage!.To.Single().Address);
        Assert.Equal("Reset your password", sentMessage.Subject);
        Assert.Contains("123456", sentMessage.Body);
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_UsedForEmailChangeConfirmation_SendsToNewAddress()
    {
        // ASP.NET Core Identity's "change email" flow calls the same SendConfirmationLinkAsync
        // method used for registration confirmation, but targeting the new, unconfirmed address.
        var smtpClientMock = new Mock<ISmtpClient>();
        MailMessage? sentMessage = null;
        smtpClientMock
            .Setup(c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, CancellationToken>((message, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        var sender = BuildSender(smtpClientMock);
        var user = MakeUser();

        await sender.SendConfirmationLinkAsync(user, "new-address@example.com", "https://learnstack.dev/confirm-email-change?code=def");

        Assert.NotNull(sentMessage);
        Assert.Equal("new-address@example.com", sentMessage!.To.Single().Address);
        Assert.Contains("https://learnstack.dev/confirm-email-change?code=def", sentMessage.Body);
    }

    [Fact]
    public async Task SendEmailAsync_WhenSmtpClientThrows_PropagatesException()
    {
        var smtpClientMock = new Mock<ISmtpClient>();
        smtpClientMock
            .Setup(c => c.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SmtpException("connection refused"));

        var sender = BuildSender(smtpClientMock);
        var user = MakeUser();

        await Assert.ThrowsAsync<SmtpException>(
            () => sender.SendConfirmationLinkAsync(user, "recipient@example.com", "https://learnstack.dev/confirm"));
    }
}
