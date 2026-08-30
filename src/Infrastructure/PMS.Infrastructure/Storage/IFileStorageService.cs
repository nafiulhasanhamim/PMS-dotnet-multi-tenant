using PMS.SharedKernel.DependencyInjection;

namespace PMS.Infrastructure.Storage;

/// <summary>
/// Abstraction for file storage operations.
/// Supports local file system, Azure Blob Storage, AWS S3, etc.
/// </summary>
public interface IFileStorageService : IScopedService
{
    /// <summary>
    /// Uploads a file.
    /// </summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="container">Optional container/folder name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URL or path to the uploaded file.</returns>
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string? container = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file from byte array.
    /// </summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="container">Optional container/folder name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URL or path to the uploaded file.</returns>
    Task<string> UploadAsync(
        byte[] content,
        string fileName,
        string? container = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file.
    /// </summary>
    /// <param name="filePath">The file path or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content stream.</returns>
    Task<Stream?> DownloadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file as byte array.
    /// </summary>
    /// <param name="filePath">The file path or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content.</returns>
    Task<byte[]?> DownloadBytesAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="filePath">The file path or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file was deleted successfully.</returns>
    Task<bool> DeleteAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="filePath">The file path or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists.</returns>
    Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a temporary URL for accessing a file (for cloud storage).
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="expiresIn">How long the URL should be valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A temporary access URL.</returns>
    Task<string?> GetTemporaryUrlAsync(
        string filePath,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);
}
