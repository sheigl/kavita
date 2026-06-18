using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Response DTO for TTS server configuration endpoints.
/// </summary>
public class TtsServerConfigDto
{
    /// <summary>
    /// Base URL of the configured TTS server (e.g. "https://api.openai.com").
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether an API key is currently configured (not the actual key).
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Default TTS model identifier.
    /// </summary>
    public string DefaultModel { get; set; } = "tts-1";

    /// <summary>
    /// Default voice identifier.
    /// </summary>
    public string DefaultVoice { get; set; } = "alloy";

    /// <summary>
    /// Default playback speed multiplier (0.5 - 4.0).
    /// </summary>
    public float DefaultSpeed { get; set; } = 1.0f;
}
