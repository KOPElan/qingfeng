using QingFeng.Models;

namespace QingFeng.Services;

/// <summary>
/// Service interface for Anydrop (云笈) functionality
/// </summary>
public interface IAnydropService
{
    /// <summary>
    /// Get messages with pagination (newest first)
    /// </summary>
    Task<List<AnydropMessage>> GetMessagesAsync(int pageSize = 20, int? beforeMessageId = null);
    
    /// <summary>
    /// Get a single message by ID with all attachments
    /// </summary>
    Task<AnydropMessage?> GetMessageByIdAsync(int messageId);
    
    /// <summary>
    /// Create a new message with optional text content
    /// </summary>
    Task<AnydropMessage> CreateMessageAsync(string content, string messageType = "Text");
    
    /// <summary>
    /// Add an attachment to a message
    /// </summary>
    Task<AnydropAttachment> AddAttachmentAsync(int messageId, string fileName, Stream fileStream, long fileSize, string contentType);
    
    /// <summary>
    /// Search messages by content
    /// </summary>
    Task<List<AnydropMessage>> SearchMessagesAsync(string searchTerm);
    
    /// <summary>
    /// Download an attachment
    /// </summary>
    Task<(byte[] FileBytes, string FileName, string ContentType)> DownloadAttachmentAsync(int attachmentId);
    
    /// <summary>
    /// Delete a message and all its attachments
    /// </summary>
    Task DeleteMessageAsync(int messageId);
    
    /// <summary>
    /// Get total message count
    /// </summary>
    Task<int> GetTotalMessageCountAsync();
    
    /// <summary>
    /// Update link metadata for a message asynchronously (non-blocking)
    /// </summary>
    Task UpdateLinkMetadataAsync(int messageId, string url);
    
    /// <summary>
    /// Create a placeholder attachment for a message (before file upload)
    /// </summary>
    Task<AnydropAttachment> CreatePlaceholderAttachmentAsync(int messageId, string fileName, long fileSize, string contentType);
    
    /// <summary>
    /// Update attachment upload status
    /// </summary>
    Task UpdateAttachmentStatusAsync(int attachmentId, string status, string? errorMessage = null);
    
    /// <summary>
    /// Upload file content to an existing attachment placeholder
    /// </summary>
    Task UploadAttachmentFileAsync(int attachmentId, Stream fileStream);
    
    /// <summary>
    /// Convert absolute paths to relative paths for all attachments (data migration utility)
    /// </summary>
    Task<int> ConvertAbsolutePathsToRelativeAsync();
    
    /// <summary>
    /// Get an attachment by ID
    /// </summary>
    Task<AnydropAttachment?> GetAttachmentByIdAsync(int attachmentId);
    
    /// <summary>
    /// Get thumbnail bytes for an attachment
    /// </summary>
    Task<byte[]?> GetThumbnailBytesAsync(int attachmentId);
}
