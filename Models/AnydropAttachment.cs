namespace QingFeng.Models;

/// <summary>
/// Represents a file attachment in an Anydrop message
/// </summary>
public class AnydropAttachment
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to AnydropMessage
    /// </summary>
    public int MessageId { get; set; }
    
    /// <summary>
    /// Original filename
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Stored file path on server
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// MIME type of the file
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Attachment type: Image, Video, Document, Other
    /// </summary>
    public string AttachmentType { get; set; } = "Other";
    
    /// <summary>
    /// Timestamp when attachment was uploaded
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Upload status: Pending, Uploading, Completed, Failed
    /// </summary>
    public string UploadStatus { get; set; } = "Pending";
    
    /// <summary>
    /// Error message if upload failed
    /// </summary>
    public string? UploadErrorMessage { get; set; }
    
    /// <summary>
    /// Navigation property to parent message
    /// </summary>
    public AnydropMessage Message { get; set; } = null!;
}
