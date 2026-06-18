namespace Kavita.Models.DTOs.SignalR;

/// <summary>
/// SignalR message containing stream progress/status updates.
/// </summary>
public class TtsStreamStatusMessage
{
    /// <summary>
    /// Zero-based index of the current chunk being processed.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Total number of chunks in this stream session.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Whether the stream has completed successfully.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Whether the stream was cancelled by the user.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Error message if the stream failed (null on success).
    /// </summary>
    public string? ErrorMessage { get; set; }
}
