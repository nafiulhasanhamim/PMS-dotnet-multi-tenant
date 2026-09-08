using PMS.Application.Common.Provisioning;

namespace PMS.WebApi.Extensions;

/// <summary>
/// Configuration for the first platform administrator, bound from the "PlatformAdmin" section.
/// </summary>
public sealed class PlatformAdminSeedOptions
{
    public const string SectionName = "PlatformAdmin";

    /// <summary>Off by default. Nothing is seeded unless this is explicitly turned on.</summary>
    public bool Enabled { get; set; }

    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = "Platform Administrator";
    public string Password { get; set; } = string.Empty;
}

public static class SeedingExtensions
{
    /// <summary>
    /// Creates the first platform administrator if configuration asks for one.
    ///
    /// Runs before the app starts serving, and is a no-op unless <c>PlatformAdmin:Enabled</c>
    /// is true. The password belongs in user-secrets or an environment variable
    /// (<c>PlatformAdmin__Password</c>) — not in appsettings.json, which is committed.
    ///
    /// A failure here does not stop the app. The system is still perfectly serviceable with an
    /// administrator that already exists, and refusing to start because a seed step failed
    /// would turn a convenience into an outage.
    /// </summary>
    public static async Task SeedPlatformAdminAsync(this WebApplication app)
    {
        var options = new PlatformAdminSeedOptions();
        app.Configuration.GetSection(PlatformAdminSeedOptions.SectionName).Bind(options);

        if (!options.Enabled)
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILogger<PlatformAdminSeeder>>();

        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogError(
                "PlatformAdmin:Enabled is true but Email or Password is missing. Nothing seeded.");
            return;
        }

        try
        {
            using var scope = app.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<PlatformAdminSeeder>();

            await seeder.EnsureAsync(options.Email, options.FullName, options.Password);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seeding the platform administrator failed.");
        }
    }
}
