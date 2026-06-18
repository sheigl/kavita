namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Granularity level for text chunking when sending text to the TTS server.
/// </summary>
public enum TtsChunkGranularity
{
    /// <summary>Split at every paragraph break.</summary>
    Paragraph = 0,

    /// <summary>Split at every sentence boundary (period, question mark, exclamation).</summary>
    Sentence = 1,
}
