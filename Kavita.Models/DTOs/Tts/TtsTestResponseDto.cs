namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Response from testing TTS server connectivity.
/// </summary>
public class TtsTestResponseDto
{
    /// <summary>
    /// Whether the test request succeeded (HTTP 200 + valid audio returned).
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
