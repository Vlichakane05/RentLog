namespace RentLog.Services;

/// <summary>
/// Utility for client-side image processing (compression, resizing).
/// Uses Blazor's canvas interop sparingly. For production, consider ImageSharp or System.Drawing.
/// </summary>
public class ImageCompressionService
{
    /// <summary>
    /// Estimate compressed size after JPEG quality reduction.
    /// This is a rough calculation; actual results depend on image content.
    /// </summary>
    public static long EstimateCompressedSize(long originalSizeBytes, int jpegQuality = 80)
    {
        // Rough heuristic: quality 80 reduces to ~60-70% of original
        // quality 90 reduces to ~80-85% of original
        double ratio = jpegQuality switch
        {
            >= 95 => 0.95,
            >= 90 => 0.85,
            >= 80 => 0.70,
            >= 70 => 0.60,
            >= 50 => 0.40,
            _ => 0.30
        };
        return (long)(originalSizeBytes * ratio);
    }

    /// <summary>
    /// Check if image meets size requirements after compression.
    /// If already under limit, return original size.
    /// </summary>
    public static bool WillFitAfterCompression(long originalSizeBytes, long maxSizeBytes)
    {
        var estimatedAfterCompression = EstimateCompressedSize(originalSizeBytes);
        return estimatedAfterCompression <= maxSizeBytes;
    }

    /// <summary>
    /// Get recommended JPEG quality to target a specific file size.
    /// Returns quality 1-100.
    /// </summary>
    public static int GetRecommendedQuality(long originalSizeBytes, long targetSizeBytes)
    {
        if (originalSizeBytes <= targetSizeBytes)
            return 95;

        var ratio = (double)targetSizeBytes / originalSizeBytes;

        return ratio switch
        {
            > 0.90 => 95,
            > 0.85 => 90,
            > 0.70 => 80,
            > 0.60 => 70,
            > 0.50 => 60,
            > 0.40 => 50,
            > 0.30 => 40,
            _ => 30
        };
    }
}
