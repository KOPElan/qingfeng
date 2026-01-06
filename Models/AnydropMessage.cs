namespace QingFeng.Models;

/// <summary>
/// Represents a message in Anydrop (云笈) - can contain text and/or attachments
/// </summary>
public class AnydropMessage
{
    public int Id { get; set; }
    
    /// <summary>
    /// Text content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of message: Text, Image, Video, Document, Audio, or File (Other)
    /// </summary>
    public string MessageType { get; set; } = "Text";
    
    /// <summary>
    /// Timestamp when message was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Extracted URL if message contains a hyperlink
    /// </summary>
    public string? LinkUrl { get; set; }
    
    /// <summary>
    /// Title extracted from the hyperlink's HTML head
    /// </summary>
    public string? LinkTitle { get; set; }
    
    /// <summary>
    /// Description extracted from the hyperlink's HTML meta tags
    /// </summary>
    public string? LinkDescription { get; set; }
    
    /// <summary>
    /// Related attachments for this message
    /// </summary>
    public ICollection<AnydropAttachment> Attachments { get; set; } = new List<AnydropAttachment>();
}
