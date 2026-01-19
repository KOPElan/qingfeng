namespace QingFeng.Services;

/// <summary>
/// Service for handling file uploads with streaming support, path validation, and sanitization.
/// Extracted from FileManagerService to be reusable across different features like FileManager and AnyDrop.
/// </summary>
public class FileUploadService : IFileUploadService
{
    private readonly ILogger<FileUploadService> _logger;
    
    // Buffer size for streaming file operations (80KB)
    // This is optimal for most scenarios as it balances memory usage and I/O performance
    private const int StreamBufferSize = 81920;

    public FileUploadService(ILogger<FileUploadService> logger)
    {
        _logger = logger;
    }

    public async Task<string> UploadFileStreamAsync(string directoryPath, string fileName, Stream fileStream, long fileSize, Func<string, bool>? pathValidator = null)
    {
        // Validate directory path if validator is provided
        if (pathValidator != null && !pathValidator(directoryPath))
        {
            throw new UnauthorizedAccessException("Access to this path is not allowed");
        }

        // Sanitize filename to prevent path traversal attacks
        var sanitizedFileName = SanitizeFileName(fileName);
        
        var fullPath = Path.Combine(directoryPath, sanitizedFileName);
        
        // Final path check after combining
        if (pathValidator != null && !pathValidator(fullPath))
        {
            throw new UnauthorizedAccessException("Access to this path is not allowed");
        }

        // Use streaming to avoid loading entire file into memory
        // This is more efficient for large files
        using (var fileStreamOut = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: StreamBufferSize, useAsync: true))
        {
            await fileStream.CopyToAsync(fileStreamOut);
        }

        _logger.LogInformation("Uploaded file via stream: {FileName} to {DirectoryPath}", sanitizedFileName, directoryPath);
        return fullPath;
    }

    public async Task<string> UploadFileAsync(string directoryPath, string fileName, byte[] content, Func<string, bool>? pathValidator = null)
    {
        // Validate directory path if validator is provided
        if (pathValidator != null && !pathValidator(directoryPath))
        {
            throw new UnauthorizedAccessException("Access to this path is not allowed");
        }

        // Sanitize filename to prevent path traversal attacks
        var sanitizedFileName = SanitizeFileName(fileName);
        
        var fullPath = Path.Combine(directoryPath, sanitizedFileName);
        
        // Final path check after combining
        if (pathValidator != null && !pathValidator(fullPath))
        {
            throw new UnauthorizedAccessException("Access to this path is not allowed");
        }

        await File.WriteAllBytesAsync(fullPath, content);

        _logger.LogInformation("Uploaded file: {FileName} to {DirectoryPath}", sanitizedFileName, directoryPath);
        return fullPath;
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be empty", nameof(fileName));
        }

        // Extract just the filename part to prevent path traversal
        var sanitized = Path.GetFileName(fileName);
        
        // Additional validation
        if (string.IsNullOrWhiteSpace(sanitized) || 
            sanitized.Contains("..") || 
            sanitized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid file name", nameof(fileName));
        }

        return sanitized;
    }

    public string GenerateUniqueFileName(string originalFileName, string? uniquePrefix = null)
    {
        var sanitized = SanitizeFileName(originalFileName);
        var extension = Path.GetExtension(sanitized);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
        
        var prefix = string.IsNullOrWhiteSpace(uniquePrefix) ? Guid.NewGuid().ToString() : uniquePrefix;
        
        return $"{fileNameWithoutExtension}_{prefix}{extension}";
    }
}
