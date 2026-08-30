using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PMS.Infrastructure.Storage;

/// <summary>
/// Local file system storage service implementation.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly HashSet<string> _allowedExtensions;

    public LocalFileStorageService(IOptions<FileStorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _allowedExtensions = string.IsNullOrWhiteSpace(_options.AllowedExtensions)
            ? []
            : _options.AllowedExtensions
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant())
                .ToHashSet();

        // Ensure base directory exists
        if (!Directory.Exists(_options.BasePath))
        {
            Directory.CreateDirectory(_options.BasePath);
        }
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string? container = null,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(fileName, content.Length);

        var filePath = GetFilePath(fileName, container);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        _logger.LogInformation("File uploaded successfully: {FilePath}", filePath);

        return filePath;
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        byte[] content,
        string fileName,
        string? container = null,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        return await UploadAsync(stream, fileName, container, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> DownloadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found: {FilePath}", fullPath);
            return null;
        }

        var memoryStream = new MemoryStream();
        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    /// <inheritdoc />
    public async Task<byte[]?> DownloadBytesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found: {FilePath}", fullPath);
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found for deletion: {FilePath}", fullPath);
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        _logger.LogInformation("File deleted successfully: {FilePath}", fullPath);

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = GetFullPath(filePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    /// <inheritdoc />
    public Task<string?> GetTemporaryUrlAsync(
        string filePath,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        // Local file storage doesn't support temporary URLs
        // Return the file path as-is
        var fullPath = GetFullPath(filePath);
        return Task.FromResult<string?>(File.Exists(fullPath) ? fullPath : null);
    }

    private void ValidateFile(string fileName, long fileSize)
    {
        if (fileSize > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File size ({fileSize} bytes) exceeds maximum allowed size ({_options.MaxFileSizeBytes} bytes).");
        }

        if (_allowedExtensions.Count > 0)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    $"File extension '{extension}' is not allowed. Allowed extensions: {_options.AllowedExtensions}");
            }
        }
    }

    private string GetFilePath(string fileName, string? container)
    {
        var safeFileName = _options.UseOriginalFileName
            ? SanitizeFileName(fileName)
            : $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";

        return string.IsNullOrEmpty(container)
            ? Path.Combine(_options.BasePath, safeFileName)
            : Path.Combine(_options.BasePath, container, safeFileName);
    }

    private string GetFullPath(string filePath)
    {
        // If the path is already absolute, return it
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        // Otherwise, combine with base path
        return Path.Combine(_options.BasePath, filePath);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrEmpty(sanitized) ? Guid.NewGuid().ToString() : sanitized;
    }
}
