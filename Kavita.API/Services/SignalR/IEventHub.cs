using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.SignalR;

namespace Kavita.API.Services.SignalR;

/// <summary>
/// Responsible for ushering events to the UI and allowing simple DI hook to send data
/// </summary>
public interface IEventHub
{
    Task SendMessageAsync(string method, SignalRMessage message, bool onlyAdmins = true, CancellationToken ct = default);
    Task SendMessageToAsync(string method, SignalRMessage message, int userId, CancellationToken ct = default);

    /// <summary>
    /// Sends a TTS audio chunk directly to the specified user via SignalR.
    /// </summary>
    Task SendTtsAudioChunk(int userId, TtsAudioChunkMessage message);

    /// <summary>
    /// Sends a TTS playback status update directly to the specified user via SignalR.
    /// </summary>
    Task SendTtsPlaybackUpdate(int userId, TtsStreamStatusMessage message);
}
