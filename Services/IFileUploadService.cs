namespace QingFeng.Services;

/// <summary>
/// Service for handling file uploads with streaming support, path validation, and sanitization.
/// Extracted from FileManagerService to be reusable across different features.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Upload a file from a stream to a specified directory with security checks.
    /// Uses streaming to avoid loading entire file into memory.
    /// </summary>
    /// <param name="directoryPath">Target directory path</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="fileStream">File content stream</param>
    /// <param name="fileSize">File size in bytes</param>
    /// <param name="pathValidator">Optional function to validate paths. If null, no validation is performed.</param>
    /// <returns>Full path of the uploaded file</returns>
    Task<string> UploadFileStreamAsync(string directoryPath, string fileName, Stream fileStream, long fileSize, Func<string, bool>? pathValidator = null);

    /// <summary>
    /// Upload a file from byte array to a specified directory with security checks.
    /// For smaller files only - consider using UploadFileStreamAsync for large files.
    /// </summary>
    /// <param name="directoryPath">Target directory path</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="content">File content as byte array</param>
    /// <param name="pathValidator">Optional function to validate paths. If null, no validation is performed.</param>
    /// <returns>Full path of the uploaded file</returns>
    Task<string> UploadFileAsync(string directoryPath, string fileName, byte[] content, Func<string, bool>? pathValidator = null);

    /// <summary>
    /// Sanitize a filename to prevent path traversal and invalid characters.
    /// </summary>
    /// <param name="fileName">Original filename</param>
    /// <returns>Sanitized filename</returns>
    string SanitizeFileName(string fileName);

    /// <summary>
    /// Generate a unique filename to avoid collisions.
    /// </summary>
    /// <param name="originalFileName">Original filename</param>
    /// <param name="uniquePrefix">Optional unique prefix (e.g., GUID or ID)</param>
    /// <returns>Unique filename</returns>
    string GenerateUniqueFileName(string originalFileName, string? uniquePrefix = null);
}
