using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using QingFeng.Data;
using QingFeng.Models;
using System.Text.RegularExpressions;

namespace QingFeng.Services;

/// <summary>
/// Service implementation for Anydrop (云笈) functionality
/// </summary>
public class AnydropService : IAnydropService
{
    private readonly IDbContextFactory<QingFengDbContext> _dbContextFactory;
    private readonly ILogger<AnydropService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _anydropStoragePath;

    public AnydropService(
        IDbContextFactory<QingFengDbContext> dbContextFactory,
        ILogger<AnydropService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        
        // Initialize storage path - try to read from settings synchronously
        _anydropStoragePath = GetStoragePathFromSettings(configuration);
        
        // Ensure storage directory exists
        if (!Directory.Exists(_anydropStoragePath))
        {
            Directory.CreateDirectory(_anydropStoragePath);
            _logger.LogInformation("Created Anydrop storage directory: {Path}", _anydropStoragePath);
        }
    }

    private string GetStoragePathFromSettings(IConfiguration configuration)
    {
        try
        {
            // Try to get from system settings using synchronous API
            using var context = _dbContextFactory.CreateDbContext();
            var setting = context.SystemSettings
                .FirstOrDefault(s => s.Key == "anydropStoragePath");
            
            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                return setting.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read Anydrop storage path from settings, using default");
        }
        
        // Fallback to configuration or default
        return configuration["AnydropStoragePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "AnydropFiles");
    }

    public async Task<List<AnydropMessage>> GetMessagesAsync(int pageSize = 20, int? beforeMessageId = null)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();

        var query = context.AnydropMessages
            .Include(m => m.Attachments)
            .AsQueryable();

        if (beforeMessageId.HasValue)
        {
            // Get messages older than the specified ID (for pagination)
            query = query.Where(m => m.Id < beforeMessageId.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(pageSize)
            .ToListAsync();
    }        

    public async Task<AnydropMessage?> GetMessageByIdAsync(int messageId)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        return await context.AnydropMessages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId);
    }

    public async Task<AnydropMessage> CreateMessageAsync(string content, string messageType = "Text")
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var message = new AnydropMessage
        {
            Content = content ?? string.Empty,
            MessageType = messageType,
            CreatedAt = DateTime.UtcNow
        };
        
        // Detect URL but don't fetch metadata yet (to avoid blocking)
        var url = ExtractUrl(content ?? string.Empty);
        if (!string.IsNullOrEmpty(url))
        {
            message.LinkUrl = url;
            // Metadata will be fetched asynchronously after message is created
        }
        
        context.AnydropMessages.Add(message);
        await context.SaveChangesAsync();
        
        _logger.LogInformation("Created Anydrop message with ID: {MessageId}", message.Id);
        return message;
    }

    public async Task<AnydropAttachment> AddAttachmentAsync(
        int messageId, 
        string fileName, 
        Stream fileStream, 
        long fileSize, 
        string contentType)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        // Verify message exists
        var message = await context.AnydropMessages.FindAsync(messageId);
        if (message == null)
        {
            throw new InvalidOperationException($"Message with ID {messageId} not found");
        }
        
        // Capture timestamp once for consistency
        var now = DateTime.UtcNow;
        
        // Get date-based directory structure
        var (dateDirectory, dateSubPath) = GetDateBasedDirectory(now);
        
        // Generate unique file name to avoid collisions
        var fileExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{messageId}_{Guid.NewGuid()}{fileExtension}";
        var absoluteFilePath = Path.Combine(dateDirectory, uniqueFileName);
        
        // Store relative path in database for portability
        var relativeFilePath = Path.Combine(dateSubPath, uniqueFileName);
        
        // Save file to disk
        using (var fileStreamWriter = new FileStream(absoluteFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(fileStreamWriter);
        }
        
        // Determine attachment type from content type
        var attachmentType = DetermineAttachmentType(contentType);
        
        var attachment = new AnydropAttachment
        {
            MessageId = messageId,
            FileName = fileName,
            FilePath = relativeFilePath, // Store relative path
            FileSize = fileSize,
            ContentType = contentType,
            AttachmentType = attachmentType,
            UploadedAt = now, // Use consistent timestamp
            UploadStatus = Models.UploadStatus.Completed // Mark as completed since file is uploaded
        };
        
        context.AnydropAttachments.Add(attachment);
        
        // Update message type to match attachment type
        // Each message should only have one type of content
        if (attachmentType != "Other")
        {
            message.MessageType = attachmentType;
        }
        else
        {
            message.MessageType = "File";
        }
        
        await context.SaveChangesAsync();
        
        _logger.LogInformation("Added attachment to message {MessageId}: {FileName} at {RelativePath}", messageId, fileName, relativeFilePath);
        return attachment;
    }

    public async Task<List<AnydropMessage>> SearchMessagesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<AnydropMessage>();
        }
        
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var lowerSearchTerm = searchTerm.ToLower();
        
        return await context.AnydropMessages
            .Include(m => m.Attachments)
            .Where(m => 
                m.Content.ToLower().Contains(lowerSearchTerm) ||
                m.Attachments.Any(a => a.FileName.ToLower().Contains(lowerSearchTerm)) ||
                (m.LinkTitle != null && m.LinkTitle.ToLower().Contains(lowerSearchTerm)) ||
                (m.LinkDescription != null && m.LinkDescription.ToLower().Contains(lowerSearchTerm)) ||
                (m.LinkUrl != null && m.LinkUrl.ToLower().Contains(lowerSearchTerm)))
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(50) // Limit search results
            .ToListAsync();
    }

    public async Task<(byte[] FileBytes, string FileName, string ContentType)> DownloadAttachmentAsync(int attachmentId)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var attachment = await context.AnydropAttachments.FindAsync(attachmentId);
        if (attachment == null)
        {
            throw new FileNotFoundException($"Attachment with ID {attachmentId} not found");
        }
        
        // Resolve the actual file path (handle both relative and absolute paths)
        var actualFilePath = GetAbsoluteFilePath(attachment.FilePath);
        
        if (!File.Exists(actualFilePath))
        {
            throw new FileNotFoundException($"File not found: {actualFilePath}");
        }
        
        var fileBytes = await File.ReadAllBytesAsync(actualFilePath);
        return (fileBytes, attachment.FileName, attachment.ContentType);
    }

    public async Task DeleteMessageAsync(int messageId)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var message = await context.AnydropMessages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        
        if (message == null)
        {
            throw new InvalidOperationException($"Message with ID {messageId} not found");
        }
        
        // Delete associated files
        foreach (var attachment in message.Attachments)
        {
            try
            {
                var absoluteFilePath = GetAbsoluteFilePath(attachment.FilePath);
                if (File.Exists(absoluteFilePath))
                {
                    File.Delete(absoluteFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file: {FilePath}", attachment.FilePath);
            }
        }
        
        context.AnydropMessages.Remove(message);
        await context.SaveChangesAsync();
        
        _logger.LogInformation("Deleted Anydrop message with ID: {MessageId}", messageId);
    }

    public async Task<int> GetTotalMessageCountAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.AnydropMessages.CountAsync();
    }

    public async Task UpdateLinkMetadataAsync(int messageId, string url)
    {
        try
        {
            // Fetch metadata
            var (title, description) = await FetchLinkMetadataAsync(url);
            
            // Update message in database
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var message = await context.AnydropMessages.FindAsync(messageId);
            
            if (message != null)
            {
                message.LinkTitle = title;
                message.LinkDescription = description;
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Updated link metadata for message {MessageId}, URL: {Url}", messageId, url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update link metadata for message {MessageId}, URL: {Url}", messageId, url);
        }
    }

    /// <summary>
    /// Convert a file path (relative or absolute) to an absolute path
    /// </summary>
    private string GetAbsoluteFilePath(string filePath)
    {
        // If already absolute, return as-is
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }
        
        // If relative, combine with storage path
        return Path.Combine(_anydropStoragePath, filePath);
    }

    /// <summary>
    /// Generate date-based subdirectory path and ensure it exists
    /// </summary>
    /// <returns>Tuple of (absolute directory path, relative path from storage root)</returns>
    private (string absoluteDir, string relativeDir) GetDateBasedDirectory(DateTime timestamp)
    {
        // Generate date-based directory structure (YYYY/MM/DD)
        var dateSubPath = Path.Combine(
            timestamp.Year.ToString(), 
            timestamp.Month.ToString("D2"), 
            timestamp.Day.ToString("D2")
        );
        var absoluteDir = Path.Combine(_anydropStoragePath, dateSubPath);
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(absoluteDir))
        {
            Directory.CreateDirectory(absoluteDir);
        }
        
        return (absoluteDir, dateSubPath);
    }

    private static string DetermineAttachmentType(string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "Image";
        }
        else if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return "Video";
        }
        else if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return "Audio";
        }
        else if (contentType.Contains("document", StringComparison.OrdinalIgnoreCase) ||
                 contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                 contentType.Contains("word", StringComparison.OrdinalIgnoreCase) ||
                 contentType.Contains("excel", StringComparison.OrdinalIgnoreCase) ||
                 contentType.Contains("powerpoint", StringComparison.OrdinalIgnoreCase))
        {
            return "Document";
        }
        else
        {
            return "Other";
        }
    }
    
    /// <summary>
    /// Extract the first URL from content
    /// </summary>
    private static string? ExtractUrl(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        
        // Regex pattern to match URLs
        var urlPattern = @"https?://[^\s<>""']+";
        var match = Regex.Match(content, urlPattern, RegexOptions.IgnoreCase);
        
        return match.Success ? match.Value : null;
    }
    
    /// <summary>
    /// Fetch title and description metadata from a URL
    /// </summary>
    private async Task<(string? Title, string? Description)> FetchLinkMetadataAsync(string url)
    {
        try
        {
            // Basic SSRF protection: validate URL scheme and reject localhost/private IPs
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") ||
                uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.StartsWith("127.") ||
                uri.Host.StartsWith("192.168.") ||
                uri.Host.StartsWith("10.") ||
                uri.Host.StartsWith("172."))
            {
                _logger.LogWarning("Rejected URL for security reasons: {Url}", url);
                return (null, null);
            }
            
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10); // Timeout after 10 seconds
            
            // Add user agent to avoid being blocked by some servers
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; QingFeng/1.0)");
            
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var html = await response.Content.ReadAsStringAsync();
            
            // Extract title from <title> tag
            var titleMatch = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
            var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : null;
            
            // Extract description from meta tags
            // Try og:description first (Open Graph)
            var ogDescMatch = Regex.Match(html, @"<meta\s+property=[""']og:description[""']\s+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            var description = ogDescMatch.Success ? ogDescMatch.Groups[1].Value.Trim() : null;
            
            // If no og:description, try standard meta description
            if (string.IsNullOrEmpty(description))
            {
                var metaDescMatch = Regex.Match(html, @"<meta\s+name=[""']description[""']\s+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                description = metaDescMatch.Success ? metaDescMatch.Groups[1].Value.Trim() : null;
            }
            
            // If still no description, try reverse order (content before name/property)
            if (string.IsNullOrEmpty(description))
            {
                var reverseDescMatch = Regex.Match(html, @"<meta\s+content=[""']([^""']+)[""']\s+(?:name|property)=[""'](?:description|og:description)[""']", RegexOptions.IgnoreCase);
                description = reverseDescMatch.Success ? reverseDescMatch.Groups[1].Value.Trim() : null;
            }
            
            return (title, description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch metadata from URL: {Url}", url);
            return (null, null);
        }
    }

    public async Task<AnydropAttachment> CreatePlaceholderAttachmentAsync(int messageId, string fileName, long fileSize, string contentType)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        // Verify message exists
        var message = await context.AnydropMessages.FindAsync(messageId);
        if (message == null)
        {
            throw new InvalidOperationException($"Message with ID {messageId} not found");
        }
        
        // Determine attachment type from content type
        var attachmentType = DetermineAttachmentType(contentType);
        
        var attachment = new AnydropAttachment
        {
            MessageId = messageId,
            FileName = fileName,
            FilePath = string.Empty, // Will be set when file is actually uploaded
            FileSize = fileSize,
            ContentType = contentType,
            AttachmentType = attachmentType,
            UploadedAt = DateTime.UtcNow,
            UploadStatus = Models.UploadStatus.Pending
        };
        
        context.AnydropAttachments.Add(attachment);
        
        // Update message type to match attachment type
        message.MessageType = attachmentType != "Other" ? attachmentType : "File";
        
        await context.SaveChangesAsync();
        
        _logger.LogInformation("Created placeholder attachment for message {MessageId}: {FileName}", messageId, fileName);
        return attachment;
    }

    public async Task UpdateAttachmentStatusAsync(int attachmentId, string status, string? errorMessage = null)
    {
        // Validate status
        if (!Models.UploadStatus.IsValid(status))
        {
            throw new ArgumentException($"Invalid upload status: {status}", nameof(status));
        }
        
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var attachment = await context.AnydropAttachments.FindAsync(attachmentId);
        if (attachment == null)
        {
            throw new InvalidOperationException($"Attachment with ID {attachmentId} not found");
        }
        
        attachment.UploadStatus = status;
        attachment.UploadErrorMessage = errorMessage;
        
        await context.SaveChangesAsync();
        
        _logger.LogInformation("Updated attachment {AttachmentId} status to {Status}", attachmentId, status);
    }

    public async Task UploadAttachmentFileAsync(int attachmentId, Stream fileStream)
    {
        // First context: Fetch attachment details
        string absoluteFilePath;
        string relativeFilePath;
        int messageId;
        string fileName;
        
        using (var context = await _dbContextFactory.CreateDbContextAsync())
        {
            var attachment = await context.AnydropAttachments.FindAsync(attachmentId);
            if (attachment == null)
            {
                throw new InvalidOperationException($"Attachment with ID {attachmentId} not found");
            }
            
            messageId = attachment.MessageId;
            fileName = attachment.FileName;
            
            // Get date-based directory structure using helper method
            var now = DateTime.UtcNow;
            var (dateDirectory, dateSubPath) = GetDateBasedDirectory(now);
            
            // Generate unique file name
            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{messageId}_{Guid.NewGuid()}{fileExtension}";
            absoluteFilePath = Path.Combine(dateDirectory, uniqueFileName);
            
            // Store relative path for database
            relativeFilePath = Path.Combine(dateSubPath, uniqueFileName);
        }
        
        // Save file to disk with error handling (outside DbContext)
        try
        {
            using (var fileStreamWriter = new FileStream(absoluteFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fileStream.CopyToAsync(fileStreamWriter);
                await fileStreamWriter.FlushAsync();
            }
            
            // Verify file was written successfully
            if (!File.Exists(absoluteFilePath))
            {
                throw new IOException($"File was not written successfully: {absoluteFilePath}");
            }
            
            var fileInfo = new FileInfo(absoluteFilePath);
            if (fileInfo.Length == 0)
            {
                File.Delete(absoluteFilePath);
                throw new IOException("File was written but has zero length");
            }
        }
        catch (Exception ex)
        {
            // Clean up file if it was partially written
            try
            {
                if (File.Exists(absoluteFilePath))
                {
                    File.Delete(absoluteFilePath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up partially written file: {FilePath}", absoluteFilePath);
            }
            
            throw new IOException($"Failed to save file: {ex.Message}", ex);
        }
        
        // Second context: Update attachment status after file is written
        using (var context = await _dbContextFactory.CreateDbContextAsync())
        {
            var attachment = await context.AnydropAttachments.FindAsync(attachmentId);
            if (attachment == null)
            {
                // File was written but attachment no longer exists - clean up
                try
                {
                    File.Delete(absoluteFilePath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up orphaned file: {FilePath}", absoluteFilePath);
                }
                throw new InvalidOperationException($"Attachment with ID {attachmentId} not found");
            }
            
            attachment.FilePath = relativeFilePath; // Store relative path
            attachment.UploadStatus = Models.UploadStatus.Completed;
            
            await context.SaveChangesAsync();
        }
        
        _logger.LogInformation("Uploaded file for attachment {AttachmentId}: {FileName} at {RelativePath}", attachmentId, fileName, relativeFilePath);
    }

    /// <summary>
    /// Convert absolute paths to relative paths for all attachments (data migration utility)
    /// </summary>
    public async Task<int> ConvertAbsolutePathsToRelativeAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var attachments = await context.AnydropAttachments.ToListAsync();
        var updatedCount = 0;
        
        // Normalize storage path for reliable comparison with error handling
        string normalizedStoragePath;
        try
        {
            normalizedStoragePath = Path.GetFullPath(_anydropStoragePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid storage path configuration: {StoragePath}", _anydropStoragePath);
            throw new InvalidOperationException($"Invalid storage path: {_anydropStoragePath}", ex);
        }
        
        foreach (var attachment in attachments)
        {
            try
            {
                // Check if path is absolute
                if (Path.IsPathRooted(attachment.FilePath))
                {
                    // Normalize the attachment path for reliable comparison
                    string normalizedAttachmentPath;
                    try
                    {
                        normalizedAttachmentPath = Path.GetFullPath(attachment.FilePath);
                    }
                    catch (Exception pathEx)
                    {
                        _logger.LogWarning(pathEx, "Invalid path format for attachment {AttachmentId}: {FilePath}", 
                            attachment.Id, attachment.FilePath);
                        continue;
                    }
                    
                    // If the file path is within the storage directory, convert to relative
                    if (normalizedAttachmentPath.StartsWith(normalizedStoragePath, StringComparison.OrdinalIgnoreCase))
                    {
                        var relativePath = Path.GetRelativePath(normalizedStoragePath, normalizedAttachmentPath);
                        
                        // Verify file exists before updating
                        if (File.Exists(normalizedAttachmentPath))
                        {
                            attachment.FilePath = relativePath;
                            updatedCount++;
                            _logger.LogInformation("Converted path to relative for attachment {AttachmentId}: {RelativePath}", 
                                attachment.Id, relativePath);
                        }
                        else
                        {
                            _logger.LogWarning("File not found for attachment {AttachmentId}: {FilePath}", 
                                attachment.Id, normalizedAttachmentPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing attachment {AttachmentId}", attachment.Id);
                // Continue processing other attachments
            }
        }
        
        if (updatedCount > 0)
        {
            await context.SaveChangesAsync();
            _logger.LogInformation("Converted {Count} attachment paths from absolute to relative", updatedCount);
        }
        
        return updatedCount;
    }
}
