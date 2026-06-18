namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Represents a single text chunk ready to be sent to the TTS server.
/// </summary>
public class TextChunkDto
{
    /// <summary>
    /// Zero-based index of this chunk in the full list.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The text content for this chunk (trimmed, non-empty).
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
