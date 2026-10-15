namespace RentLog.Services;

/// <summary>
/// Represents a media file for upload (photo or video).
/// </summary>
public class MediaFile
{
    /// <summary>
    /// Unique ID for this media file (generated on client, persists through upload).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Original file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME type (e.g., "image/jpeg", "video/mp4").
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Base64-encoded file data for upload.
    /// </summary>
    public string Base64Data { get; set; } = string.Empty;

    /// <summary>
    /// Thumbnail preview: base64-encoded small image.
    /// </summary>
    public string ThumbnailBase64 { get; set; } = string.Empty;

    /// <summary>
    /// For videos: duration in seconds.
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Optional alt text for accessibility.
    /// </summary>
    public string AltText { get; set; } = string.Empty;

    /// <summary>
    /// URL returned by server after successful upload.
    /// </summary>
    public string? UploadedUrl { get; set; }
}

/// <summary>
/// Media upload service interface.
/// Abstracts photo and video upload, handling compression and validation.
/// </summary>
public interface IMediaService
{
    /// <summary>
    /// Upload a media file (photo or video) asynchronously.
    /// </summary>
    /// <param name="media">Media file to upload.</param>
    /// <param name="progress">Progress callback (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>URL of uploaded media, or null if upload failed.</returns>
    Task<string?> UploadAsync(
        MediaFile media,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate photo before upload: type, size, metadata.
    /// </summary>
    /// <returns>Error message, or null if valid.</returns>
    string? ValidatePhoto(string mimeType, long fileSizeBytes);

    /// <summary>
    /// Validate video before upload: type, size, duration.
    /// </summary>
    /// <returns>Error message, or null if valid.</returns>
    string? ValidateVideo(string mimeType, long fileSizeBytes, double? durationSeconds);

    /// <summary>
    /// Get max file size in bytes for photos.
    /// </summary>
    long MaxPhotoSizeBytes { get; }

    /// <summary>
    /// Get max file size in bytes for videos.
    /// </summary>
    long MaxVideoSizeBytes { get; }

    /// <summary>
    /// Get max video duration in seconds.
    /// </summary>
    double MaxVideoDurationSeconds { get; }
}
