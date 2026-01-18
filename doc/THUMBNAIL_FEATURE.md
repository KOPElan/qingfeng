# Thumbnail Generation Feature

## Overview

The thumbnail generation feature automatically creates thumbnails for images and videos uploaded to AnyDrop. Thumbnails significantly improve loading performance and reduce bandwidth requirements when previewing media files.

## Features

- **Image Thumbnails**: Automatically resize images to 300x300 pixels while preserving aspect ratio
- **Video Thumbnails**: Extract frame at 1 second (or mid-point for short videos) from uploaded videos
- **Smart Fallback**: Falls back to original file if thumbnail generation fails
- **Optimized Storage**: Thumbnails stored as JPEG with 85% quality for optimal size/quality balance
- **Reusable Service**: IThumbnailService can be used by FileManager and other components

## Technical Details

### Dependencies

- **SixLabors.ImageSharp** v3.1.12 - For image processing and thumbnail generation
- **FFMpegCore** v5.4.0 - For video frame extraction
- **FFmpeg** (system dependency) - Required for video thumbnail generation

### Architecture

1. **IThumbnailService**: Interface defining thumbnail generation operations
2. **ThumbnailService**: Implementation handling both image and video thumbnails
3. **AnydropService**: Integrates thumbnail generation during file upload
4. **Database**: ThumbnailPath field added to AnydropAttachment model

### Storage

- Thumbnails are stored alongside original files in date-based directory structure
- Naming convention: `{messageId}_{guid}_thumb.jpg`
- All thumbnails are saved as JPEG format regardless of source format

## Installation & Configuration

### Installing FFmpeg

FFmpeg is required for video thumbnail generation. Image thumbnails work without FFmpeg.

#### Ubuntu/Debian
```bash
sudo apt update
sudo apt install ffmpeg
```

#### CentOS/RHEL
```bash
sudo yum install epel-release
sudo yum install ffmpeg
```

#### macOS
```bash
brew install ffmpeg
```

#### Windows
Download from https://ffmpeg.org/download.html and add to PATH

#### Docker
Add to your Dockerfile:
```dockerfile
RUN apt-get update && apt-get install -y ffmpeg
```

### Verification

Verify FFmpeg is installed correctly:
```bash
ffmpeg -version
```

## Usage

### Automatic Thumbnail Generation

Thumbnails are generated automatically when files are uploaded through AnyDrop:

1. User uploads image or video
2. File is saved to storage
3. ThumbnailService checks if file type supports thumbnails
4. For images: Resize to thumbnail size
5. For videos: Extract frame and resize
6. Thumbnail saved alongside original file
7. ThumbnailPath stored in database

### API Endpoints

#### Get Thumbnail
```
GET /api/anydrop/attachment/{attachmentId}/thumbnail
```

Returns:
- Thumbnail image (JPEG) if available
- Falls back to original file if thumbnail doesn't exist
- HTTP 404 if attachment not found

#### Get Original File
```
GET /api/anydrop/attachment/{attachmentId}/preview
```

Returns the original file (not thumbnail)

### UI Integration

The Anydrop UI automatically uses thumbnails for:
- Image grid view
- Video preview thumbnails (with play icon overlay)
- Message attachment previews

## Configuration

### Thumbnail Size

Default size is 300x300 pixels. To customize, modify the calls to thumbnail generation:

```csharp
await thumbnailService.GenerateThumbnailAsync(
    sourcePath, 
    outputPath, 
    contentType,
    maxWidth: 400,   // Custom width
    maxHeight: 400   // Custom height
);
```

### JPEG Quality

Default quality is 85%. To adjust, modify ThumbnailService.cs:

```csharp
var encoder = new JpegEncoder
{
    Quality = 90 // Adjust between 1-100
};
```

## Troubleshooting

### Video Thumbnails Not Generating

**Symptom**: Images work but video thumbnails don't appear

**Solution**:
1. Check FFmpeg is installed: `ffmpeg -version`
2. Check application logs for FFmpeg-related errors
3. Verify FFmpeg is in system PATH
4. For Docker, ensure FFmpeg is in the container

### Poor Thumbnail Quality

**Symptom**: Thumbnails look pixelated or blurry

**Solution**:
1. Increase JPEG quality setting (default is 85)
2. Increase thumbnail dimensions
3. Check source file quality

### Thumbnails Not Appearing

**Symptom**: No thumbnails shown in UI

**Solution**:
1. Check browser console for 404 errors
2. Verify thumbnail files exist in storage directory
3. Check file permissions on thumbnail directory
4. Review application logs for generation errors

### High Storage Usage

**Symptom**: Thumbnail storage consuming too much space

**Solution**:
1. Reduce JPEG quality setting
2. Reduce thumbnail dimensions
3. Implement cleanup task for orphaned thumbnails

## Performance Considerations

### Bandwidth Savings

- Typical image: 2-5 MB → thumbnail: 20-50 KB (40-100x reduction)
- Typical video: 50-200 MB → thumbnail: 30-60 KB (1000-6000x reduction)

### Generation Time

- Images: < 100ms for most files
- Videos: 500ms - 2s depending on file size and duration

### Async Processing

Thumbnail generation happens asynchronously during upload:
1. File is uploaded and saved
2. Placeholder shown in UI
3. Thumbnail generates in background
4. UI updates when complete

## Future Enhancements

- [ ] Background thumbnail regeneration for existing files
- [ ] Configurable thumbnail sizes via system settings
- [ ] PDF thumbnail generation (first page preview)
- [ ] Document thumbnails (Word, Excel, etc.)
- [ ] Integration with FileManager for indexed directories
- [ ] Batch thumbnail generation admin tool
- [ ] Thumbnail cache warming on startup
- [ ] WebP format support for better compression

## Security Considerations

- Input validation on file types
- Path traversal prevention
- Resource limits to prevent DoS
- FFmpeg subprocess isolation
- Graceful handling of malformed media files

## Related Files

- `Services/IThumbnailService.cs` - Service interface
- `Services/ThumbnailService.cs` - Implementation
- `Services/AnydropService.cs` - Integration with file upload
- `Models/AnydropAttachment.cs` - Database model with ThumbnailPath
- `Endpoints/AnydropEndpoints.cs` - Thumbnail API endpoint
- `Components/Pages/Anydrop.razor` - UI implementation
