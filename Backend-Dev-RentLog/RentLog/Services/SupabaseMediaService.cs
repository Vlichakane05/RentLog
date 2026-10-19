using RentLog.Models;
using Supabase.Storage;
using System.Collections.Concurrent;

namespace RentLog.Services;

public sealed class SupabaseMediaService(SupabaseClientProvider clientProvider) : IMediaService
{
    private const long PhotoMaxSize = 10 * 1024 * 1024;
    private const long VideoMaxSize = 50 * 1024 * 1024;
    private const double VideoMaxDuration = 30;
    private const string BucketName = "issue-media";

    private static readonly HashSet<string> PhotoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/heic"
    };

    private static readonly HashSet<string> VideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/quicktime"
    };
    private readonly ConcurrentDictionary<string, UploadedMediaMetadata> _uploadedMedia = new(StringComparer.Ordinal);

    internal bool TryGetUploadedMetadata(string storagePath, out UploadedMediaMetadata metadata) =>
        _uploadedMedia.TryGetValue(storagePath, out metadata!);

    internal void ClearUploadedMetadata(IEnumerable<string> storagePaths)
    {
        foreach (var storagePath in storagePaths)
        {
            _uploadedMedia.TryRemove(storagePath, out _);
        }
    }

    public long MaxPhotoSizeBytes => PhotoMaxSize;
    public long MaxVideoSizeBytes => VideoMaxSize;
    public double MaxVideoDurationSeconds => VideoMaxDuration;

    public string? ValidatePhoto(string mimeType, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(mimeType) || !PhotoMimeTypes.Contains(mimeType))
        {
            return "Invalid photo type. Accepted: JPG, PNG, HEIC.";
        }

        if (fileSizeBytes <= 0)
        {
            return "File is empty.";
        }

        return fileSizeBytes > PhotoMaxSize
            ? "Photo too large. Maximum size is 10 MB."
            : null;
    }

    public string? ValidateVideo(string mimeType, long fileSizeBytes, double? durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(mimeType) || !VideoMimeTypes.Contains(mimeType))
        {
            return "Invalid video type. Accepted: MP4, MOV.";
        }

        if (fileSizeBytes <= 0)
        {
            return "File is empty.";
        }

        if (fileSizeBytes > VideoMaxSize)
        {
            return "Video too large. Maximum size is 50 MB.";
        }

        if (durationSeconds is > VideoMaxDuration)
        {
            return "Video too long. Maximum duration is 30 seconds.";
        }

        if (durationSeconds is null)
        {
            return "Video duration could not be verified. Select the video again and retry.";
        }

        return null;
    }

    public async Task<string?> UploadAsync(
        MediaFile media,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);
        cancellationToken.ThrowIfCancellationRequested();

        var validationError = media.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? ValidatePhoto(media.MimeType, media.FileSizeBytes)
            : ValidateVideo(media.MimeType, media.FileSizeBytes, media.DurationSeconds);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        var userId = (await clientProvider.GetClientAsync()).Auth.CurrentUser?.Id;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new InvalidOperationException("Sign in before uploading evidence.");
        }

        var content = media.Base64Data;
        if (content.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var separator = content.IndexOf(',');
            if (separator < 0)
            {
                throw new InvalidDataException("The selected media data is not a valid data URL.");
            }

            content = content[(separator + 1)..];
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(content);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The selected media data could not be decoded.", exception);
        }

        var extension = media.MimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/heic" => ".heic",
            "video/mp4" => ".mp4",
            "video/quicktime" => ".mov",
            _ => throw new InvalidOperationException($"Unsupported media type '{media.MimeType}'.")
        };

        var fileStem = new string(Path.GetFileNameWithoutExtension(media.FileName)
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .Take(80)
            .ToArray());
        if (string.IsNullOrWhiteSpace(fileStem))
        {
            fileStem = "evidence";
        }

        var originalFileName = $"{fileStem}{extension}";
        var storagePath = $"pending/{parsedUserId:D}/{Guid.NewGuid():N}-{originalFileName}";
        var client = await clientProvider.GetClientAsync();
        var bucket = client.Storage.From(BucketName);
        await bucket.UploadOrResume(
            bytes,
            storagePath,
            new Supabase.Storage.FileOptions
            {
                ContentType = media.MimeType,
                CacheControl = "3600",
                Upsert = false
            },
            onProgress: (_, value) => progress?.Report((int)value),
            cancellationToken: cancellationToken);

        _uploadedMedia[storagePath] = new UploadedMediaMetadata(
            Path.GetFileName(media.FileName),
            media.MimeType,
            bytes.LongLength,
            media.DurationSeconds,
            media.AltText);
        progress?.Report(100);
        return storagePath;
    }
}

internal sealed record UploadedMediaMetadata(
    string FileName,
    string MimeType,
    long FileSizeBytes,
    double? DurationSeconds,
    string AltText);
