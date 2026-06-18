using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Tts;

namespace Kavita.API.Services;

/// <summary>
/// Service for Text-to-Speech audiobook functionality. Handles EPUB text extraction,
/// TTS server communication, and SignalR-based audio streaming.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Gets or creates the TTS configuration for a user.
    /// </summary>
    Task<TtsServerConfigDto> GetUserTtsConfigAsync(int userId);

    /// <summary>
    /// Saves or updates the TTS server configuration for a user. Encrypts the API key at rest.
    /// </summary>
    Task SaveUserTtsConfigAsync(int userId, string serverUrl, string apiKey, string defaultModel, string defaultVoice, float defaultSpeed);

    /// <summary>
    /// Tests connectivity to the TTS server by sending a short text sample.
    /// </summary>
    Task<TtsTestResponseDto> TestTtsServerAsync(TtsTestRequestDto request);

    /// <summary>
    /// Retrieves available voices from the configured TTS server.
    /// </summary>
    Task<IList<TtsVoiceDto>> GetVoicesAsync(int userId);

    /// <summary>
    /// Extracts text chunks from an EPUB chapter using the specified granularity.
    /// </summary>
    Task<TextChunksResponseDto> GetTextChunksAsync(int chapterId, TtsChunkGranularity granularity);

    /// <summary>
    /// Starts streaming audio for a range of text chunks via SignalR.
    /// Returns immediately; audio chunks are pushed to the client via SignalR.
    /// </summary>
    Task StartStreamAsync(int userId, int chapterId, TtsStreamRequest request);

    /// <summary>
    /// Stops an active streaming session for a user.
    /// </summary>
    Task StopStreamAsync(int userId);
}
