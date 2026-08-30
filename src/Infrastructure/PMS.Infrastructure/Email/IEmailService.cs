using PMS.SharedKernel.DependencyInjection;

namespace PMS.Infrastructure.Email;

/// <summary>
/// Abstraction for sending emails.
/// </summary>
public interface IEmailService : ITransientService
{
    /// <summary>
    /// Sends an email.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body (HTML supported).</param>
    /// <param name="isHtml">Whether the body is HTML.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the email was sent successfully.</returns>
    Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email to multiple recipients.
    /// </summary>
    /// <param name="to">Recipient email addresses.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body (HTML supported).</param>
    /// <param name="isHtml">Whether the body is HTML.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the email was sent successfully.</returns>
    Task<bool> SendAsync(
        IEnumerable<string> to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email with attachments.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body (HTML supported).</param>
    /// <param name="attachments">Email attachments.</param>
    /// <param name="isHtml">Whether the body is HTML.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the email was sent successfully.</returns>
    Task<bool> SendWithAttachmentsAsync(
        string to,
        string subject,
        string body,
        IEnumerable<EmailAttachment> attachments,
        bool isHtml = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an email attachment.
/// </summary>
public class EmailAttachment
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the file content.
    /// </summary>
    public required byte[] Content { get; set; }

    /// <summary>
    /// Gets or sets the content type (MIME type).
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";
}
