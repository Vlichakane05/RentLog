namespace RentLog.Services;

/// <summary>
/// Mock implementation of IMediaService for development and testing.
/// Simulates upload with artificial delays and progress reporting.
/// </summary>
public class MockMediaService : IMediaService
{
    // Photo constraints
    private const long PhotoMaxSize = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] PhotoMimeTypes = { "image/jpeg", "image/png", "image/heic" };

    // Video constraints
    private const long VideoMaxSize = 50 * 1024 * 1024; // 50 MB
    private const double VideoMaxDuration = 30.0; // seconds
    private static readonly string[] VideoMimeTypes = { "video/mp4", "video/quicktime" };

    public long MaxPhotoSizeBytes => PhotoMaxSize;
    public long MaxVideoSizeBytes => VideoMaxSize;
    public double MaxVideoDurationSeconds => VideoMaxDuration;

    public string? ValidatePhoto(string mimeType, long fileSizeBytes)
    {
        if (string.IsNullOrEmpty(mimeType))
            return "File type is required.";

        if (!PhotoMimeTypes.Contains(mimeType.ToLower()))
            return $"Invalid photo type. Accepted: JPG, PNG, HEIC.";

        if (fileSizeBytes > PhotoMaxSize)
            return $"Photo too large. Max 10 MB, got {FormatBytes(fileSizeBytes)}.";

        if (fileSizeBytes == 0)
            return "File is empty.";

        return null;
    }

    public string? ValidateVideo(string mimeType, long fileSizeBytes, double? durationSeconds)
    {
        if (string.IsNullOrEmpty(mimeType))
            return "File type is required.";

        if (!VideoMimeTypes.Contains(mimeType.ToLower()))
            return "Invalid video type. Accepted: MP4, MOV.";

        if (fileSizeBytes > VideoMaxSize)
            return $"Video too large. Max 50 MB, got {FormatBytes(fileSizeBytes)}.";

        if (durationSeconds.HasValue && durationSeconds.Value > VideoMaxDuration)
            return $"Video too long. Max 30 seconds, got {durationSeconds.Value:F1}s.";

        if (fileSizeBytes == 0)
            return "File is empty.";

        return null;
    }

    public async Task<string?> UploadAsync(
        MediaFile media,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate upload with progress reporting
        const int steps = 10;
        const int delayMs = 200;

        for (int i = 1; i <= steps; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return null;

            progress?.Report((i * 100) / steps);
            await Task.Delay(delayMs, cancellationToken);
        }

        // Simulate successful upload: return a mock URL
        var fileName = Path.GetFileNameWithoutExtension(media.FileName);
        var extension = Path.GetExtension(media.FileName);
        var uploadedUrl = $"https://example.supabase.co/storage/v1/object/public/media/{Guid.NewGuid()}{extension}";

        return uploadedUrl;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024} KB";
        return $"{bytes / (1024 * 1024)} MB";
    }
}
