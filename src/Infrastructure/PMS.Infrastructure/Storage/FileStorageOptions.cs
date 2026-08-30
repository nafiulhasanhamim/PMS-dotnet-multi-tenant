namespace PMS.Infrastructure.Storage;

/// <summary>
/// Configuration options for file storage service.
/// </summary>
public class FileStorageOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Gets or sets the base path for local file storage.
    /// </summary>
    public string BasePath { get; set; } = "uploads";

    /// <summary>
    /// Gets or sets the maximum file size in bytes.
    /// Default is 10 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the allowed file extensions (comma-separated).
    /// Empty means all extensions are allowed.
    /// </summary>
    public string AllowedExtensions { get; set; } = ".jpg,.jpeg,.png,.gif,.pdf,.doc,.docx,.xls,.xlsx";

    /// <summary>
    /// Gets or sets whether to use the original file name or generate a unique one.
    /// </summary>
    public bool UseOriginalFileName { get; set; } = false;
}
