namespace QingFeng.Models;

/// <summary>
/// Upload status constants for file attachments
/// </summary>
public static class UploadStatus
{
    /// <summary>
    /// Attachment is waiting to be uploaded
    /// </summary>
    public const string Pending = "Pending";
    
    /// <summary>
    /// Attachment is currently being uploaded
    /// </summary>
    public const string Uploading = "Uploading";
    
    /// <summary>
    /// Attachment has been successfully uploaded
    /// </summary>
    public const string Completed = "Completed";
    
    /// <summary>
    /// Attachment upload has failed
    /// </summary>
    public const string Failed = "Failed";
    
    /// <summary>
    /// Validates if a status string is valid
    /// </summary>
    public static bool IsValid(string? status)
    {
        return !string.IsNullOrEmpty(status) && 
               (status == Pending || status == Uploading || status == Completed || status == Failed);
    }
}
