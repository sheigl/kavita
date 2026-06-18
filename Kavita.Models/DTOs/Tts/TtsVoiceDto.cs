namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Represents a voice option returned by the TTS server.
/// </summary>
public class TtsVoiceDto
{
    /// <summary>
    /// Unique identifier for this voice (e.g. "alloy", "nova").
    /// </summary>
    public string VoiceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the voice.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
