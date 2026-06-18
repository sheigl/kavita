using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.Tts;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using VersOne.Epub;

namespace Kavita.Services;

/// <summary>
/// Implements TTS audiobook functionality: EPUB text extraction, TTS server communication,
/// and SignalR-based audio streaming.
/// </summary>
public class TtsService(
    IUnitOfWork unitOfWork,
    ILogger<TtsService> logger,
    IHttpClientFactory httpClientFactory,
    IEventHub eventHub,
    IDataProtector dataProtector)
    : ITtsService
{
    private const string HttpClientName = "TtsClient";
    private const string TtsApiKeyPurpose = "Kavita.TtsApiKey";

    /// <summary>
    /// Encrypts an API key using ASP.NET Core Data Protection.
    /// </summary>
    private string EncryptApiKey(string apiKey) => dataProtector.Protect(apiKey);

    /// <summary>
    /// Decrypts an API key using ASP.NET Core Data Protection.
    /// </summary>
    private string? DecryptApiKey(string encrypted)
    {
        try
        {
            return dataProtector.Unprotect(encrypted);
        }
        catch
        {
            logger.LogWarning("Failed to decrypt TTS API key. Key may have been rotated or corrupted.");
            return null;
        }
    }

    /// <summary>
    /// Active streaming tasks keyed by userId for cancellation support.
    /// </summary>
    private static readonly ConcurrentDictionary<int, CancellationTokenSource> ActiveStreams = new ();

    public async Task<TtsServerConfigDto> GetUserTtsConfigAsync(int userId)
    {
        var config = await unitOfWork.UserRepository.GetUserTtsConfigAsync(userId);

        if (config == null)
        {
            return new TtsServerConfigDto();
        }

        return new TtsServerConfigDto
        {
            ServerUrl = config.ServerUrl,
            HasApiKey = !string.IsNullOrEmpty(config.ApiKeyEncrypted),
            DefaultModel = config.DefaultModel,
            DefaultVoice = config.DefaultVoice,
            DefaultSpeed = config.DefaultSpeed,
        };
    }

    public async Task SaveUserTtsConfigAsync(int userId, string serverUrl, string apiKey, string defaultModel, string defaultVoice, float defaultSpeed)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new ArgumentException("Server URL is required.", nameof(serverUrl));
        }

        // Strip trailing slash for consistency
        var cleanUrl = serverUrl.TrimEnd('/');

        string? encryptedKey = null;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            encryptedKey = EncryptApiKey(apiKey);
        }

        var config = await unitOfWork.UserRepository.GetUserTtsConfigAsync(userId);

        if (config == null)
        {
            config = new UserTtsConfig
            {
                AppUserId = userId,
            };
            unitOfWork.UserRepository.AddUserTtsConfig(config);
        }

        config.ServerUrl = cleanUrl;
        config.ApiKeyEncrypted = encryptedKey;
        config.DefaultModel = defaultModel;
        config.DefaultVoice = defaultVoice;
        config.DefaultSpeed = defaultSpeed;

        await unitOfWork.CommitAsync();
    }

    public async Task<TtsTestResponseDto> TestTtsServerAsync(TtsTestRequestDto request)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            client.BaseAddress = new Uri(request.ServerUrl.TrimEnd('/'));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);

            var body = new
            {
                model = request.Model,
                input = "This is a test of the text-to-speech server.",
                voice = request.Voice,
                response_format = "mp3",
            };

            var content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync("/v1/audio/speech", content);

            if (response.IsSuccessStatusCode)
            {
                var audioData = await response.Content.ReadAsByteArrayAsync();
                bool isValidAudio = audioData.Length > 0 && IsLikelyAudio(audioData);

                return new TtsTestResponseDto
                {
                    Success = true,
                    Message = isValidAudio
                        ? "Successfully connected and received valid audio."
                        : "Connected but response may not be valid audio.",
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new TtsTestResponseDto
            {
                Success = false,
                Message = $"Server returned {(int)response.StatusCode}: {errorContent}",
            };
        }
        catch (HttpRequestException ex)
        {
            return new TtsTestResponseDto
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}",
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return new TtsTestResponseDto
            {
                Success = false,
                Message = "Request timed out. Check the server URL.",
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TTS test failed for user request to {ServerUrl}", request.ServerUrl);
            return new TtsTestResponseDto
            {
                Success = false,
                Message = $"Unexpected error: {ex.Message}",
            };
        }
    }

    public async Task<IList<TtsVoiceDto>> GetVoicesAsync(int userId)
    {
        var config = await unitOfWork.UserRepository.GetUserTtsConfigAsync(userId);
        if (config == null || string.IsNullOrEmpty(config.ServerUrl))
        {
            return Array.Empty<TtsVoiceDto>();
        }

        // OpenAI-compatible servers may not expose a voices endpoint.
        // Return well-known default voices as a fallback.
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            client.BaseAddress = new Uri(config.ServerUrl.TrimEnd('/'));

            string? apiKey = DecryptApiKey(config);
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            // Try to fetch voices from the server (some providers support this)
            using var response = await client.GetAsync("/v1/audio/voices");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var voices = ParseVoicesResponse(content);
                if (voices.Count > 0)
                {
                    return voices;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch voices from TTS server for user {UserId}. Using defaults.", userId);
        }

        // Fallback: well-known OpenAI TTS voices
        return new List<TtsVoiceDto>
        {
            new() { VoiceId = "alloy", Name = "Alloy" },
            new() { VoiceId = "echo", Name = "Echo" },
            new() { VoiceId = "fable", Name = "Fable" },
            new() { VoiceId = "onyx", Name = "Onyx" },
            new() { VoiceId = "nova", Name = "Nova" },
            new() { VoiceId = "shimmer", Name = "Shimmer" },
        };
    }

    public async Task<TextChunksResponseDto> GetTextChunksAsync(int chapterId, TtsChunkGranularity granularity)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null)
        {
            throw new InvalidOperationException($"Chapter {chapterId} not found.");
        }

        string fullText;
        using (var epub = await EpubReader.OpenBookAsync(chapter.Files.First().FilePath, BookService.LenientBookReaderOptions))
        {
            fullText = await ExtractFullTextAsync(epub);
        }

        var chunks = granularity switch
        {
            TtsChunkGranularity.Paragraph => SplitByParagraph(fullText),
            TtsChunkGranularity.Sentence => SplitBySentence(fullText),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
        };

        return new TextChunksResponseDto
        {
            Chunks = chunks.Select((text, index) => new TextChunkDto
            {
                Index = index,
                Text = text.Trim(),
            }).ToList(),
        };
    }

    public async Task StartStreamAsync(int userId, int chapterId, TtsStreamRequest request)
    {
        // Cancel any existing stream for this user
        await StopStreamAsync(userId);

        var cts = new CancellationTokenSource();
        ActiveStreams[userId] = cts;
        var token = cts.Token;

        logger.LogInformation("Starting TTS stream for user {UserId}, chapter {ChapterId}, chunks {Start}-{End}",
            userId, chapterId, request.StartChunk, request.EndChunk);

        try
        {
            var config = await unitOfWork.UserRepository.GetUserTtsConfigAsync(userId);
            if (config == null || string.IsNullOrEmpty(config.ServerUrl))
            {
                throw new InvalidOperationException("TTS server not configured. Please set up your TTS settings first.");
            }

            // Get text chunks
            var chunksResponse = await GetTextChunksAsync(chapterId, request.Granularity);
            var chunks = chunksResponse.Chunks;

            if (!chunks.Any())
            {
                throw new InvalidOperationException("No text content found in this chapter.");
            }

            // Determine chunk range
            int start = request.StartChunk;
            int end = request.EndChunk < 0 ? chunks.Count - 1 : Math.Min(request.EndChunk, chunks.Count - 1);

            if (start >= chunks.Count || start > end)
            {
                throw new ArgumentOutOfRangeException(nameof(request.StartChunk), "Invalid chunk range.");
            }

            // Resolve voice and speed with fallback chain: request override -> profile override -> user default
            string voice = request.VoiceOverride ?? config.DefaultVoice;
            float speed = request.SpeedOverride ?? config.DefaultSpeed;

            // Get reading profile for this series to check for overrides
            var seriesId = await unitOfWork.ChapterRepository.GetSeriesIdForChapter(chapterId);
            if (seriesId.HasValue)
            {
                var profile = await unitOfWork.UserRepository.GetUserReadingProfileForSeriesAsync(userId, seriesId.Value);
                if (profile != null && profile.TtsEnabled)
                {
                    if (!string.IsNullOrEmpty(request.VoiceOverride))
                        voice = request.VoiceOverride;
                    else if (!string.IsNullOrEmpty(profile.TtsVoiceOverride))
                        voice = profile.TtsVoiceOverride;

                    speed = request.SpeedOverride ?? profile.TtsSpeedOverride ?? config.DefaultSpeed;
                }
            }

            string? apiKey = DecryptApiKey(config);
            var client = httpClientFactory.CreateClient(HttpClientName);
            client.BaseAddress = new Uri(config.ServerUrl.TrimEnd('/'));
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            int totalChunks = end - start + 1;
            int processedCount = 0;

            for (int i = start; i <= end; i++)
            {
                if (token.IsCancellationRequested)
                {
                    await eventHub.SendTtsPlaybackUpdate(userId, new TtsStreamStatusMessage
                    {
                        ChunkIndex = i,
                        TotalChunks = totalChunks,
                        IsComplete = false,
                        IsCancelled = true,
                    });
                    return;
                }

                var chunk = chunks[i];
                processedCount++;

                try
                {
                    var audioData = await SynthesizeChunkAsync(client, config.DefaultModel, voice, speed, chunk.Text, token);

                    // Send audio chunk via SignalR
                    await eventHub.SendTtsAudioChunk(userId, new TtsAudioChunkMessage
                    {
                        ChunkIndex = i,
                        AudioBase64 = Convert.ToBase64String(audioData),
                    });

                    // Send progress update
                    await eventHub.SendTtsPlaybackUpdate(userId, new TtsStreamStatusMessage
                    {
                        ChunkIndex = i,
                        TotalChunks = totalChunks,
                        IsComplete = false,
                        IsCancelled = false,
                    });
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("TTS stream cancelled for user {UserId}, chapter {ChapterId} at chunk {ChunkIndex}",
                        userId, chapterId, i);

                    await eventHub.SendTtsPlaybackUpdate(userId, new TtsStreamStatusMessage
                    {
                        ChunkIndex = i,
                        TotalChunks = totalChunks,
                        IsComplete = false,
                        IsCancelled = true,
                    });
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to synthesize chunk {ChunkIndex} for user {UserId}", i, userId);
                    await eventHub.SendTtsPlaybackUpdate(userId, new TtsStreamStatusMessage
                    {
                        ChunkIndex = i,
                        TotalChunks = totalChunks,
                        IsComplete = false,
                        ErrorMessage = $"Failed to synthesize chunk: {ex.Message}",
                    });
                }
            }

            // Stream complete
            logger.LogInformation("TTS stream completed for user {UserId}, chapter {ChapterId}, processed {Processed}/{Total} chunks",
                userId, chapterId, processedCount, totalChunks);

            await eventHub.SendTtsPlaybackUpdate(userId, new TtsStreamStatusMessage
            {
                ChunkIndex = end,
                TotalChunks = totalChunks,
                IsComplete = true,
                IsCancelled = false,
            });
        }
        finally
        {
            ActiveStreams.TryRemove(userId, out _);
            cts.Dispose();
        }
    }

    public async Task StopStreamAsync(int userId)
    {
        if (ActiveStreams.TryRemove(userId, out var cts))
        {
            logger.LogInformation("Stopping TTS stream for user {UserId}", userId);

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
            finally
            {
                cts.Dispose();
            }

            await eventHub.SendTtsPlaybackUpdate(userId, new TtsStreamStatusMessage
            {
                IsComplete = false,
                IsCancelled = true,
            });
        }
    }

    #region Private Helpers

    private async Task<string> ExtractFullTextAsync(EpubBookRef epub)
    {
        var sb = new StringBuilder();

        foreach (var contentFileRef in await epub.GetReadingOrderAsync())
        {
            if (contentFileRef.ContentType != EpubContentType.XHTML_1_1)
                continue;

            try
            {
                string html = await contentFileRef.ReadContentAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Skip cover pages and navigation documents
                var body = doc.DocumentNode?.SelectSingleNode("//body");
                if (body == null) continue;

                string text = body.InnerText;
                if (string.IsNullOrWhiteSpace(text)) continue;

                sb.AppendLine(text);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract text from EPUB content item: {Key}", contentFileRef.Key);
            }
        }

        return sb.ToString();
    }

    private static List<string> SplitByParagraph(string text)
    {
        // Split on double newlines or HTML paragraph boundaries
        var paragraphs = Regex.Split(text, @"\r?\n\s*\r?\n|<\s*/?p\s*>", RegexOptions.IgnoreCase);
        return paragraphs
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    private static List<string> SplitBySentence(string text)
    {
        // Split on sentence-ending punctuation followed by space or newline
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+(?=[A-Z""'(])");
        return sentences
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private async Task<byte[]> SynthesizeChunkAsync(
        HttpClient client, string model, string voice, float speed, string text, CancellationToken token)
    {
        var body = new
        {
            model = model,
            input = text,
            voice = voice,
            response_format = "mp3",
            speed = speed,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/v1/audio/speech", content, token);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(token);
    }

    private string? DecryptApiKey(UserTtsConfig config)
    {
        if (string.IsNullOrEmpty(config.ApiKeyEncrypted))
            return null;

        return DecryptApiKey(config.ApiKeyEncrypted);
    }

    private static bool IsLikelyAudio(byte[] data)
    {
        // Check for MP3 ID3 header or sync word, or WAV header
        if (data.Length < 4) return false;

        // MP3: ID3 tag "ID3" at start
        if (data[0] == 'I' && data[1] == 'D' && data[2] == '3') return true;

        // WAV: "RIFF" header
        if (data.Length >= 4 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F') return true;

        // MP3 sync word (first 11 bits = 1)
        if ((data[0] & 0xFF) == 0xFF && (data[1] & 0xE0) == 0xE0) return true;

        return data.Length > 100; // At least some data returned
    }

    private static List<TtsVoiceDto> ParseVoicesResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var voices = new List<TtsVoiceDto>();

            // Try common response formats
            if (doc.RootElement.TryGetProperty("voices", out var voicesElem) && voicesElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in voicesElem.EnumerateArray())
                {
                    voices.Add(new TtsVoiceDto
                    {
                        VoiceId = GetPropertyOrNull(v, "id") ?? GetPropertyOrNull(v, "voice_id") ?? string.Empty,
                        Name = GetPropertyOrNull(v, "name") ?? GetPropertyOrNull(v, "voice") ?? string.Empty,
                    });
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in doc.RootElement.EnumerateArray())
                {
                    voices.Add(new TtsVoiceDto
                    {
                        VoiceId = GetPropertyOrNull(v, "id") ?? GetPropertyOrNull(v, "voice_id") ?? string.Empty,
                        Name = GetPropertyOrNull(v, "name") ?? GetPropertyOrNull(v, "voice") ?? string.Empty,
                    });
                }
            }

            return voices;
        }
        catch
        {
            return new List<TtsVoiceDto>();
        }
    }

    private static string? GetPropertyOrNull(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return null;
    }

    #endregion
}


