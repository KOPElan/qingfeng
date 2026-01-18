using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using FFMpegCore;

namespace QingFeng.Services;

/// <summary>
/// Service for generating and managing thumbnails for images and videos
/// </summary>
public class ThumbnailService : IThumbnailService
{
    private readonly ILogger<ThumbnailService> _logger;
    private readonly IConfiguration _configuration;

    public ThumbnailService(ILogger<ThumbnailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> GenerateImageThumbnailAsync(string sourceFilePath, string outputFilePath, int maxWidth = 300, int maxHeight = 300)
    {
        try
        {
            if (!File.Exists(sourceFilePath))
            {
                _logger.LogWarning("Source image file not found: {SourcePath}", sourceFilePath);
                return false;
            }

            // Ensure output directory exists
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Load, resize, and save the image
            using var image = await Image.LoadAsync(sourceFilePath);
            
            // Calculate new dimensions while preserving aspect ratio
            var (newWidth, newHeight) = CalculateThumbnailSize(image.Width, image.Height, maxWidth, maxHeight);
            
            // Resize the image
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(newWidth, newHeight),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            // Save as JPEG with quality optimization
            var encoder = new JpegEncoder
            {
                Quality = 85 // Good balance between quality and file size
            };
            
            await image.SaveAsJpegAsync(outputFilePath, encoder);
            
            _logger.LogInformation("Generated image thumbnail: {OutputPath} ({Width}x{Height})", 
                outputFilePath, newWidth, newHeight);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image thumbnail from {SourcePath} to {OutputPath}", 
                sourceFilePath, outputFilePath);
            return false;
        }
    }

    public async Task<bool> GenerateVideoThumbnailAsync(string sourceFilePath, string outputFilePath, int maxWidth = 300, int maxHeight = 300)
    {
        try
        {
            if (!File.Exists(sourceFilePath))
            {
                _logger.LogWarning("Source video file not found: {SourcePath}", sourceFilePath);
                return false;
            }

            // Check if FFmpeg is available
            if (!await IsFFmpegAvailableAsync())
            {
                _logger.LogWarning("FFmpeg is not available. Video thumbnail generation is disabled.");
                return false;
            }

            // Ensure output directory exists
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Create a temporary file for the full-size frame
            var tempFramePath = Path.Combine(Path.GetTempPath(), $"thumb_{Guid.NewGuid()}.jpg");

            try
            {
                // Extract frame at 1 second (or earlier for short videos)
                var mediaInfo = await FFProbe.AnalyseAsync(sourceFilePath);
                // Ensure we don't capture at 0 seconds, and don't exceed half the video duration
                var captureTime = TimeSpan.FromSeconds(Math.Min(1, Math.Max(0.1, mediaInfo.Duration.TotalSeconds / 2)));

                // Extract the frame
                await FFMpeg.SnapshotAsync(sourceFilePath, tempFramePath, null, captureTime);

                // Now resize the extracted frame to create thumbnail
                using var image = await Image.LoadAsync(tempFramePath);
                
                var (newWidth, newHeight) = CalculateThumbnailSize(image.Width, image.Height, maxWidth, maxHeight);
                
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(newWidth, newHeight),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));

                var encoder = new JpegEncoder { Quality = 85 };
                await image.SaveAsJpegAsync(outputFilePath, encoder);

                _logger.LogInformation("Generated video thumbnail: {OutputPath} ({Width}x{Height})", 
                    outputFilePath, newWidth, newHeight);
                
                return true;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempFramePath))
                {
                    try
                    {
                        File.Delete(tempFramePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Failed to delete temporary frame file: {TempPath}", tempFramePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate video thumbnail from {SourcePath} to {OutputPath}", 
                sourceFilePath, outputFilePath);
            return false;
        }
    }

    public async Task<bool> GenerateThumbnailAsync(string sourceFilePath, string outputFilePath, string contentType, int maxWidth = 300, int maxHeight = 300)
    {
        if (!SupportsThumbnails(contentType))
        {
            _logger.LogDebug("Thumbnail generation not supported for content type: {ContentType}", contentType);
            return false;
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return await GenerateImageThumbnailAsync(sourceFilePath, outputFilePath, maxWidth, maxHeight);
        }
        else if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return await GenerateVideoThumbnailAsync(sourceFilePath, outputFilePath, maxWidth, maxHeight);
        }

        return false;
    }

    public bool SupportsThumbnails(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        // Support common image formats
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            // Most image formats are supported by ImageSharp
            return true;
        }

        // Support common video formats
        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculate thumbnail dimensions while preserving aspect ratio
    /// </summary>
    private (int width, int height) CalculateThumbnailSize(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
    {
        if (originalWidth <= maxWidth && originalHeight <= maxHeight)
        {
            return (originalWidth, originalHeight);
        }

        var ratioX = (double)maxWidth / originalWidth;
        var ratioY = (double)maxHeight / originalHeight;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = (int)(originalWidth * ratio);
        var newHeight = (int)(originalHeight * ratio);

        return (newWidth, newHeight);
    }

    /// <summary>
    /// Check if FFmpeg is available on the system
    /// </summary>
    private async Task<bool> IsFFmpegAvailableAsync()
    {
        try
        {
            // Try to get FFmpeg version to verify it's installed
            var ffmpegPath = GlobalFFOptions.GetFFMpegBinaryPath();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                // Try to find FFmpeg in PATH
                return await Task.Run(() =>
                {
                    try
                    {
                        using var process = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "ffmpeg",
                                Arguments = "-version",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        process.Start();
                        process.WaitForExit(5000); // 5 second timeout
                        return process.ExitCode == 0;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check FFmpeg availability");
            return false;
        }
    }
}
