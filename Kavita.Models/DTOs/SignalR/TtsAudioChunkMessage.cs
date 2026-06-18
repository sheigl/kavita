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
}
