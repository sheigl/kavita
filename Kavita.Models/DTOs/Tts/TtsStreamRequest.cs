using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Request body to start a TTS streaming session for an EPUB chapter.
/// </summary>
public class TtsStreamRequest
{
    /// <summary>
    /// Zero-based index of the first chunk to synthesize.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int StartChunk { get; set; }

    /// <summary>
    /// Zero-based index of the last chunk to synthesize (inclusive). Use -1 for "all remaining".
    /// </summary>
    [Range(-1, int.MaxValue)]
    public int EndChunk { get; set; } = -1;

    /// <summary>
    /// Granularity used when chunks were generated. Must match the granularity used in GetTextChunks.
    /// </summary>
    public TtsChunkGranularity Granularity { get; set; } = TtsChunkGranularity.Paragraph;

    /// <summary>
    /// Optional voice override for this session. Falls back to profile/user defaults if null.
    /// </summary>
    [MaxLength(256)]
    public string? VoiceOverride { get; set; }

    /// <summary>
    /// Optional speed override (0.5 - 4.0) for this session. Falls back to profile/user defaults if null.
    /// </summary>
    [Range(0.5f, 4.0f)]
    public float? SpeedOverride { get; set; }
}
