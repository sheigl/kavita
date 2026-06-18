using System.Collections.Generic;

namespace Kavita.Models.DTOs.Tts;

/// <summary>
/// Response containing all text chunks extracted from an EPUB chapter.
/// </summary>
public class TextChunksResponseDto
{
    /// <summary>
    /// List of text chunks in reading order.
    /// </summary>
    public IList<TextChunkDto> Chunks { get; set; } = new List<TextChunkDto>();

    /// <summary>
    /// Total number of chunks returned.
    /// </summary>
    public int TotalChunks => Chunks.Count;
}
