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
        
        // Get Anydrop storage path from configuration or use default
        _anydropStoragePath = configuration["AnydropStoragePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "AnydropFiles");
        
        // Ensure storage directory exists
        if (!Directory.Exists(_anydropStoragePath))
        {
            Directory.CreateDirectory(_anydropStoragePath);
            _logger.LogInformation("Created Anydrop storage directory: {Path}", _anydropStoragePath);
        }
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
        
        // Generate unique file name to avoid collisions
        var fileExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{messageId}_{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(_anydropStoragePath, uniqueFileName);
        
        // Save file to disk
        using (var fileStreamWriter = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(fileStreamWriter);
        }
        
        // Determine attachment type from content type
        var attachmentType = DetermineAttachmentType(contentType);
        
        var attachment = new AnydropAttachment
        {
            MessageId = messageId,
            FileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            ContentType = contentType,
            AttachmentType = attachmentType,
            UploadedAt = DateTime.UtcNow,
            UploadStatus = "Completed" // Mark as completed since file is uploaded
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
        
        _logger.LogInformation("Added attachment to message {MessageId}: {FileName}", messageId, fileName);
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
        
        if (!File.Exists(attachment.FilePath))
        {
            throw new FileNotFoundException($"File not found: {attachment.FilePath}");
        }
        
        var fileBytes = await File.ReadAllBytesAsync(attachment.FilePath);
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
                if (File.Exists(attachment.FilePath))
                {
                    File.Delete(attachment.FilePath);
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
            FilePath = string.Empty, // Will be set when file is uploaded
            FileSize = fileSize,
            ContentType = contentType,
            AttachmentType = attachmentType,
            UploadedAt = DateTime.UtcNow,
            UploadStatus = "Pending"
        };
        
        context.AnydropAttachments.Add(attachment);
        
        // Update message type to match attachment type
        if (attachmentType != "Other")
        {
            message.MessageType = attachmentType;
        }
        else
        {
            message.MessageType = "File";
        }
        
        await context.SaveChangesAsync();
        
        _logger.LogInformation("Created placeholder attachment for message {MessageId}: {FileName}", messageId, fileName);
        return attachment;
    }

    public async Task UpdateAttachmentStatusAsync(int attachmentId, string status, string? errorMessage = null)
    {
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
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var attachment = await context.AnydropAttachments.FindAsync(attachmentId);
        if (attachment == null)
        {
            throw new InvalidOperationException($"Attachment with ID {attachmentId} not found");
        }
        
        // Generate unique file name
        var fileExtension = Path.GetExtension(attachment.FileName);
        var uniqueFileName = $"{attachment.MessageId}_{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(_anydropStoragePath, uniqueFileName);
        
        // Save file to disk
        using (var fileStreamWriter = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(fileStreamWriter);
        }
        
        // Update attachment with file path and status
        attachment.FilePath = filePath;
        attachment.UploadStatus = "Completed";
        
        await context.SaveChangesAsync();
        
        _logger.LogInformation("Uploaded file for attachment {AttachmentId}: {FileName}", attachmentId, attachment.FileName);
    }
}
