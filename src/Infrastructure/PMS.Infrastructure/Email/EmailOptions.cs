namespace PMS.Infrastructure.Email;

/// <summary>
/// Configuration options for email service.
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Email";

    /// <summary>
    /// Gets or sets the SMTP server host.
    /// </summary>
    public string SmtpHost { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the SMTP server port.
    /// </summary>
    public int SmtpPort { get; set; } = 25;

    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    public string FromAddress { get; set; } = "noreply@example.com";

    /// <summary>
    /// Gets or sets the sender display name.
    /// </summary>
    public string FromName { get; set; } = "PMS";

    /// <summary>
    /// Gets or sets the SMTP username (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the SMTP password (optional).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets whether to use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Gets or sets whether email sending is enabled.
    /// When disabled, emails are logged but not sent.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
