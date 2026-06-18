using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Services;
using Kavita.Models.DTOs.Tts;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// API endpoints for Text-to-Speech audiobook configuration and streaming.
/// </summary>
[Route("api/[controller]")]
public class TtsController(
    ITtsService ttsService,
    ILocalizationService localizationService)
    : BaseApiController
{
    /// <summary>
    /// GET /api/tts/config — Get the current user's TTS server configuration.
    /// </summary>
    [HttpGet("config")]
    [Authorize]
    public async Task<ActionResult<TtsServerConfigDto>> GetTtsConfig()
    {
        var config = await ttsService.GetUserTtsConfigAsync(UserId);
        return Ok(config);
    }

    /// <summary>
    /// PUT /api/tts/config — Save or update the user's TTS server configuration.
    /// </summary>
    [HttpPut("config")]
    [Authorize]
    public async Task<ActionResult> SaveTtsConfig([FromBody] TtsSaveConfigRequest request)
    {
        try
        {
            await ttsService.SaveUserTtsConfigAsync(
                UserId,
                request.ServerUrl,
                request.ApiKey ?? string.Empty,
                request.DefaultModel,
                request.DefaultVoice,
                request.DefaultSpeed);

            var message = await localizationService.TranslateAsync(UserId, "tts.config.saved");
            return Ok(new { Message = message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// POST /api/tts/test — Test connectivity to the configured TTS server.
    /// </summary>
    [HttpPost("test")]
    [Authorize]
    public async Task<ActionResult<TtsTestResponseDto>> TestTtsServer([FromBody] TtsTestRequestDto request)
    {
        var result = await ttsService.TestTtsServerAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/tts/voices — Get available voices from the configured TTS server.
    /// </summary>
    [HttpGet("voices")]
    [Authorize]
    public async Task<ActionResult<IList<TtsVoiceDto>>> GetVoices()
    {
        var voices = await ttsService.GetVoicesAsync(UserId);
        return Ok(voices);
    }

    /// <summary>
    /// GET /api/tts/chunks?chapterId={id} — Extract text chunks from an EPUB chapter.
    /// </summary>
    [HttpGet("chunks")]
    [ChapterAccess]
    public async Task<ActionResult<TextChunksResponseDto>> GetTextChunks(
        [FromQuery] int chapterId,
        [FromQuery] TtsChunkGranularity granularity = TtsChunkGranularity.Paragraph)
    {
        var chunks = await ttsService.GetTextChunksAsync(chapterId, granularity);
        return Ok(chunks);
    }

    /// <summary>
    /// POST /api/tts/stream?chapterId={id} — Start streaming audio for a range of text chunks via SignalR.
    /// </summary>
    [HttpPost("stream")]
    [ChapterAccess]
    public async Task<ActionResult> StartStream(
        [FromQuery] int chapterId,
        [FromBody] TtsStreamRequest request)
    {
        try
        {
            await ttsService.StartStreamAsync(UserId, chapterId, request);
            var message = await localizationService.TranslateAsync(UserId, "tts.stream.started");
            return Ok(new { Message = message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// POST /api/tts/stream/stop — Stop an active streaming session.
    /// </summary>
    [HttpPost("stream/stop")]
    [Authorize]
    public async Task<ActionResult> StopStream()
    {
        await ttsService.StopStreamAsync(UserId);
        var message = await localizationService.TranslateAsync(UserId, "tts.stream.stopped");
        return Ok(new { Message = message });
    }
}

/// <summary>
/// Request body for saving TTS configuration.
/// </summary>
public class TtsSaveConfigRequest
{
    [Required]
    [MaxLength(512)]
    public string ServerUrl { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? ApiKey { get; set; }

    [Required]
    [MaxLength(256)]
    public string DefaultModel { get; set; } = "tts-1";

    [Required]
    [MaxLength(256)]
    public string DefaultVoice { get; set; } = "alloy";

    [Range(0.5f, 4.0f)]
    public float DefaultSpeed { get; set; } = 1.0f;
}
