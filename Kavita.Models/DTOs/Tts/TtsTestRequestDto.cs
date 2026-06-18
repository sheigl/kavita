using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Request body for testing TTS server connectivity.
/// </summary>
public class TtsTestRequestDto
{
    /// <summary>
    /// Base URL of the TTS server to test (e.g. "https://api.openai.com").
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication.
    /// </summary>
    [Required]
    [MaxLength(1024)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// TTS model to use (e.g. "tts-1").
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Model { get; set; } = "tts-1";

    /// <summary>
    /// Voice identifier to use for the test (e.g. "alloy").
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Voice { get; set; } = "alloy";
}
