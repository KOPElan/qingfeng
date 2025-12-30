using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;

namespace QingFeng.Services;

/// <summary>
/// Service implementation for Anydrop (云笈) functionality
/// </summary>
public class AnydropService : IAnydropService
{
    private readonly IDbContextFactory<QingFengDbContext> _dbContextFactory;
    private readonly ILogger<AnydropService> _logger;
    private readonly string _anydropStoragePath;

    public AnydropService(
        IDbContextFactory<QingFengDbContext> dbContextFactory,
        ILogger<AnydropService> logger,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        
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
            UploadedAt = DateTime.UtcNow
        };
        
        context.AnydropAttachments.Add(attachment);
        
        // Update message type if needed
        if (message.MessageType == "Text" && attachmentType != "Other")
        {
            message.MessageType = attachmentType;
        }
        else if (message.MessageType != "Mixed" && !string.IsNullOrEmpty(message.Content))
        {
            message.MessageType = "Mixed";
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
                m.Attachments.Any(a => a.FileName.ToLower().Contains(lowerSearchTerm)))
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
}
