using System.ComponentModel.DataAnnotations;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Models.Entities.User;

/// <summary>
/// Stores per-user TTS server configuration including encrypted API key.
/// One row per user.
/// </summary>
public class UserTtsConfig : IHasConcurrencyToken
{
    public int Id { get; set; }

    [Required]
    public int AppUserId { get; set; }

    /// <summary>
    /// Base URL of the TTS server (e.g. "https://api.openai.com"). Trailing slash stripped on save.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key encrypted at rest using ASP.NET Core Data Protection.
    /// </summary>
    [MaxLength(1024)]
    public string? ApiKeyEncrypted { get; set; }

    /// <summary>
    /// Default TTS model identifier (e.g. "tts-1"). Used when the user does not override per-series.
    /// </summary>
    [MaxLength(256)]
    public string DefaultModel { get; set; } = "tts-1";

    /// <summary>
    /// Default voice identifier (e.g. "alloy"). Used when the user does not override per-series.
    /// </summary>
    [MaxLength(256)]
    public string DefaultVoice { get; set; } = "alloy";

    /// <summary>
    /// Default playback speed multiplier (0.5 - 4.0). Used when the user does not override per-series.
    /// </summary>
    public float DefaultSpeed { get; set; } = 1.0f;

    /// <inheritdoc />
    [ConcurrencyCheck]
    public uint RowVersion { get; private set; }

    /// <inheritdoc />
    public void OnSavingChanges()
    {
        RowVersion++;
    }

    // Navigation property
    public AppUser AppUser { get; set; } = null!;
}
