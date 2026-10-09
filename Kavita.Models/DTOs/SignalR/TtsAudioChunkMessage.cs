namespace Kavita.Models.DTOs.SignalR;

/// <summary>
/// SignalR message containing a base64-encoded audio chunk.
/// </summary>
public class TtsAudioChunkMessage
{
    /// <summary>
    /// Zero-based index of this chunk in the stream.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Base64-encoded audio data for this chunk.
    /// </summary>
    public string AudioBase64 { get; set; } = string.Empty;

    /// <summary>
    /// The original text content for this chunk. Used by the frontend to
    /// highlight the passage currently being read aloud.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
