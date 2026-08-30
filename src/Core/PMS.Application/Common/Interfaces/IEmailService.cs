using PMS.SharedKernel.DependencyInjection;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Service for sending emails.
/// Registered as Transient lifetime.
/// </summary>
public interface IEmailService : ITransientService
{
    /// <summary>
    /// Sends an email asynchronously.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body (HTML supported).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email to multiple recipients.
    /// </summary>
    /// <param name="recipients">List of recipient email addresses.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body (HTML supported).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendEmailAsync(IEnumerable<string> recipients, string subject, string body, CancellationToken cancellationToken = default);
}
