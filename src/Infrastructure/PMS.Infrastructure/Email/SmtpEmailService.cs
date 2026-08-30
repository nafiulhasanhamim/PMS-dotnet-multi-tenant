using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PMS.Infrastructure.Email;

/// <summary>
/// SMTP-based email service implementation.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        return SendAsync([to], subject, body, isHtml, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(
        IEnumerable<string> to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        var recipients = to.ToList();

        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Email sending is disabled. Would have sent email to {Recipients} with subject: {Subject}",
                string.Join(", ", recipients), subject);
            return true;
        }

        try
        {
            using var message = CreateMessage(recipients, subject, body, isHtml);
            await SendMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Email sent successfully to {Recipients} with subject: {Subject}",
                string.Join(", ", recipients), subject);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email to {Recipients} with subject: {Subject}",
                string.Join(", ", recipients), subject);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendWithAttachmentsAsync(
        string to,
        string subject,
        string body,
        IEnumerable<EmailAttachment> attachments,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Email sending is disabled. Would have sent email to {Recipient} with subject: {Subject} and {AttachmentCount} attachments",
                to, subject, attachments.Count());
            return true;
        }

        try
        {
            using var message = CreateMessage([to], subject, body, isHtml);

            foreach (var attachment in attachments)
            {
                var stream = new MemoryStream(attachment.Content);
                message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }

            await SendMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Email with attachments sent successfully to {Recipient} with subject: {Subject}",
                to, subject);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email with attachments to {Recipient} with subject: {Subject}",
                to, subject);
            return false;
        }
    }

    private MailMessage CreateMessage(List<string> recipients, string subject, string body, bool isHtml)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        return message;
    }

    private async Task SendMessageAsync(MailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl
        };

        if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
