namespace PMS.SharedKernel.DependencyInjection;

/// <summary>
/// Marker interface for services that should be registered with Transient lifetime.
/// Transient services are created each time they are requested.
/// Use for lightweight, stateless services.
/// </summary>
/// <remarks>
/// <para>
/// <b>Usage Pattern:</b> Service interfaces should inherit from this marker interface,
/// not the implementation class. This ensures the lifetime is defined at the contract level.
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// // Define service interface that inherits from marker
/// public interface IEmailService : ITransientService
/// {
///     Task SendEmailAsync(string to, string subject, string body);
/// }
///
/// // Implementation only implements the service interface
/// public class EmailService : IEmailService
/// {
///     public Task SendEmailAsync(string to, string subject, string body) { ... }
/// }
/// </code>
/// </para>
/// </remarks>
public interface ITransientService
{
}
