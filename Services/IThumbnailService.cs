namespace QingFeng.Services;

/// <summary>
/// Service interface for generating and managing thumbnails for images and videos
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Generate a thumbnail for an image file
    /// </summary>
    /// <param name="sourceFilePath">Path to the source image file</param>
    /// <param name="outputFilePath">Path where the thumbnail should be saved</param>
    /// <param name="maxWidth">Maximum width of the thumbnail</param>
    /// <param name="maxHeight">Maximum height of the thumbnail</param>
    /// <returns>True if thumbnail was generated successfully</returns>
    Task<bool> GenerateImageThumbnailAsync(string sourceFilePath, string outputFilePath, int maxWidth = 300, int maxHeight = 300);
    
    /// <summary>
    /// Generate a thumbnail for a video file (extracts first frame)
    /// </summary>
    /// <param name="sourceFilePath">Path to the source video file</param>
    /// <param name="outputFilePath">Path where the thumbnail should be saved</param>
    /// <param name="maxWidth">Maximum width of the thumbnail</param>
    /// <param name="maxHeight">Maximum height of the thumbnail</param>
    /// <returns>True if thumbnail was generated successfully</returns>
    Task<bool> GenerateVideoThumbnailAsync(string sourceFilePath, string outputFilePath, int maxWidth = 300, int maxHeight = 300);
    
    /// <summary>
    /// Generate a thumbnail for a file based on its content type
    /// </summary>
    /// <param name="sourceFilePath">Path to the source file</param>
    /// <param name="outputFilePath">Path where the thumbnail should be saved</param>
    /// <param name="contentType">MIME type of the file</param>
    /// <param name="maxWidth">Maximum width of the thumbnail</param>
    /// <param name="maxHeight">Maximum height of the thumbnail</param>
    /// <returns>True if thumbnail was generated successfully</returns>
    Task<bool> GenerateThumbnailAsync(string sourceFilePath, string outputFilePath, string contentType, int maxWidth = 300, int maxHeight = 300);
    
    /// <summary>
    /// Check if a file type supports thumbnail generation
    /// </summary>
    /// <param name="contentType">MIME type of the file</param>
    /// <returns>True if thumbnails can be generated for this file type</returns>
    bool SupportsThumbnails(string contentType);
}
