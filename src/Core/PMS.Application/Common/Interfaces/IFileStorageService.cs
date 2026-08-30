using PMS.SharedKernel.DependencyInjection;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Service for file storage operations.
/// Registered as Scoped lifetime.
/// </summary>
public interface IFileStorageService : IScopedService
{
    /// <summary>
    /// Uploads a file and returns the file path/URL.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="content">The file content as a stream.</param>
    /// <param name="contentType">The MIME type of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path or URL where the file was stored.</returns>
    Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file by its path.
    /// </summary>
    /// <param name="filePath">The path of the file to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content as a stream.</returns>
    Task<Stream> DownloadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file by its path.
    /// </summary>
    /// <param name="filePath">The path of the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default);
}
