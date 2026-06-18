using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.Tts;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Net;

namespace Kavita.Services.Tests.Tts;

public class TtsServiceTests : IDisposable
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TtsService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEventHub _eventHub;
    private readonly IDataProtector _dataProtector;
    private readonly string _tempDir;

    public TtsServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<TtsService>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _eventHub = Substitute.For<IEventHub>();

        // Use a real in-memory DataProtection instance for testing
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kavita-test-keys-" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(_tempDir);
        var dpProvider = Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create(
            new System.IO.DirectoryInfo(_tempDir), options => options.SetApplicationName("kavita-tests"));
        _dataProtector = dpProvider.CreateProtector("TtsApiKey");
    }

    /// <summary>
    /// Creates a TtsService with an HttpClient backed by the provided HttpMessageHandler mock.
    /// This is the standard .NET pattern for testing code that uses HttpClient, since
    /// HttpClient methods like PostAsync/GetAsync are non-virtual and cannot be mocked directly.
    /// </summary>
    private TtsService CreateTtsService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        _httpClientFactory.CreateClient("TtsClient").Returns(httpClient);
        return new TtsService(_unitOfWork, _logger, _httpClientFactory, _eventHub, _dataProtector);
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region GetUserTtsConfigAsync Tests

    [Fact]
    public async Task GetUserTtsConfigAsync_ReturnsDto_WhenConfigExists()
    {
        // Arrange
        var userId = 1;
        var expectedConfig = new UserTtsConfig
        {
            Id = 1,
            AppUserId = userId,
            ServerUrl = "https://api.openai.com",
            ApiKeyEncrypted = "encrypted-key",
            DefaultModel = "tts-1",
            DefaultVoice = "alloy",
            DefaultSpeed = 1.0f
        };

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns(expectedConfig);

        // We need a handler for the service constructor, but this test doesn't use HTTP
        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.GetUserTtsConfigAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://api.openai.com", result.ServerUrl);
        Assert.True(result.HasApiKey);
        Assert.Equal("tts-1", result.DefaultModel);
        Assert.Equal("alloy", result.DefaultVoice);
        Assert.Equal(1.0f, result.DefaultSpeed);

        await _unitOfWork.UserRepository.Received(1)
            .GetUserTtsConfigAsync(userId);
    }

    [Fact]
    public async Task GetUserTtsConfigAsync_ReturnsEmptyDto_WhenNotExists()
    {
        // Arrange
        var userId = 999;

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns((UserTtsConfig?)null);

        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.GetUserTtsConfigAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.ServerUrl);
        Assert.False(result.HasApiKey);
    }

    #endregion

    #region SaveUserTtsConfigAsync Tests

    [Fact]
    public async Task SaveUserTtsConfigAsync_AddsNewConfig_WhenNoneExists()
    {
        // Arrange
        var userId = 1;

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns((UserTtsConfig?)null);

        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act
        await ttsService.SaveUserTtsConfigAsync(
            userId, "https://api.openai.com", "test-api-key", "tts-1-hd", "nova", 0.9f);

        // Assert
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveUserTtsConfigAsync_UpdatesExistingConfig()
    {
        // Arrange
        var userId = 1;
        var existingConfig = new UserTtsConfig
        {
            Id = 1,
            AppUserId = userId,
            ServerUrl = "https://old-server.com",
            DefaultModel = "tts-1",
            DefaultVoice = "alloy",
            DefaultSpeed = 1.0f
        };

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns(existingConfig);

        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act
        await ttsService.SaveUserTtsConfigAsync(
            userId, "https://api.openai.com", "new-api-key", "tts-1-hd", "nova", 0.9f);

        // Assert
        Assert.Equal("https://api.openai.com", existingConfig.ServerUrl);
        Assert.Equal("tts-1-hd", existingConfig.DefaultModel);
        Assert.Equal("nova", existingConfig.DefaultVoice);
        Assert.Equal(0.9f, existingConfig.DefaultSpeed);

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveUserTtsConfigAsync_ThrowsWhenServerUrlEmpty()
    {
        // Arrange
        var userId = 1;
        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ttsService.SaveUserTtsConfigAsync(userId, "", "key", "model", "voice", 1.0f));
    }

    [Fact]
    public async Task SaveUserTtsConfigAsync_StripsTrailingSlash()
    {
        // Arrange
        var userId = 1;
        var existingConfig = new UserTtsConfig { Id = 1, AppUserId = userId };

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns(existingConfig);

        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act
        await ttsService.SaveUserTtsConfigAsync(
            userId, "https://api.openai.com/", "key", "model", "voice", 1.0f);

        // Assert
        Assert.Equal("https://api.openai.com", existingConfig.ServerUrl);
    }

    #endregion

    #region TestTtsServerAsync Tests

    [Fact]
    public async Task TestTtsServerAsync_ReturnsSuccess_WhenServerRespondsWithAudio()
    {
        // Arrange
        var request = new TtsTestRequestDto
        {
            ServerUrl = "https://api.openai.com",
            ApiKey = "test-key",
            Model = "tts-1",
            Voice = "alloy"
        };

        // Simulate MP3 ID3 header response
        var audioBytes = new byte[] { (byte)'I', (byte)'D', (byte)'3', 0x00, 0x01, 0x02, 0x03, 0x04 };
        var handler = new MockHttpMessageHandler((requestMessage, cancellationToken) =>
        {
            Assert.Contains("/v1/audio/speech", requestMessage.RequestUri?.ToString() ?? "");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.ByteArrayContent(audioBytes)
            });
        });

        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.TestTtsServerAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task TestTtsServerAsync_ReturnsFailure_WhenUnauthorized()
    {
        // Arrange
        var request = new TtsTestRequestDto
        {
            ServerUrl = "https://api.openai.com",
            ApiKey = "bad-key",
            Model = "tts-1",
            Voice = "alloy"
        };

        var handler = new MockHttpMessageHandler((requestMessage, cancellationToken) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new System.Net.Http.StringContent("Invalid API key")
            });
        });

        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.TestTtsServerAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task TestTtsServerAsync_ReturnsFailure_WhenConnectionFails()
    {
        // Arrange
        var request = new TtsTestRequestDto
        {
            ServerUrl = "https://invalid-url.test",
            ApiKey = "key",
            Model = "tts-1",
            Voice = "alloy"
        };

        var handler = new MockHttpMessageHandler((requestMessage, cancellationToken) =>
        {
            throw new HttpRequestException("Connection refused");
        });

        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.TestTtsServerAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    #endregion

    #region GetVoicesAsync Tests

    [Fact]
    public async Task GetVoicesAsync_ReturnsEmptyList_WhenNoConfig()
    {
        // Arrange
        var userId = 1;

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns((UserTtsConfig?)null);

        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.GetVoicesAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVoicesAsync_ReturnsFallbackVoices_WhenServerFails()
    {
        // Arrange
        var userId = 1;
        var config = new UserTtsConfig
        {
            Id = 1,
            AppUserId = userId,
            ServerUrl = "https://api.openai.com",
            ApiKeyEncrypted = _dataProtector.Protect("test-key")
        };

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns(config);

        // Handler that throws to simulate server failure
        var handler = new MockHttpMessageHandler((requestMessage, cancellationToken) =>
        {
            throw new HttpRequestException("Server not reachable");
        });

        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.GetVoicesAsync(userId);

        // Assert - should return fallback voices
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetVoicesAsync_ReturnsVoicesFromServer_WhenSuccessful()
    {
        // Arrange
        var userId = 1;
        var config = new UserTtsConfig
        {
            Id = 1,
            AppUserId = userId,
            ServerUrl = "https://api.openai.com",
            ApiKeyEncrypted = _dataProtector.Protect("test-key")
        };

        _unitOfWork.UserRepository
            .GetUserTtsConfigAsync(userId)
            .Returns(config);

        var voicesJson = @"{
            ""voices"": [
                { ""id"": ""kokoro-1"", ""name"": ""Kokoro Alpha"" },
                { ""id"": ""kokoro-2"", ""name"": ""Kokoro Beta"" }
            ]
        }";

        var handler = new MockHttpMessageHandler((requestMessage, cancellationToken) =>
        {
            Assert.Contains("/v1/audio/voices", requestMessage.RequestUri?.ToString() ?? "");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(voicesJson)
            });
        });

        var ttsService = CreateTtsService(handler);

        // Act
        var result = await ttsService.GetVoicesAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("kokoro-1", result[0].VoiceId);
        Assert.Equal("Kokoro Alpha", result[0].Name);
    }

    #endregion

    #region GetTextChunksAsync Tests

    [Fact]
    public async Task GetTextChunksAsync_ThrowsWhenChapterNotFound()
    {
        // Arrange
        var chapterId = 999;

        _unitOfWork.ChapterRepository
            .GetChapterAsync(chapterId)
            .Returns((Kavita.Models.Entities.Chapter?)null);

        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ttsService.GetTextChunksAsync(chapterId, TtsChunkGranularity.Paragraph));
    }

    #endregion

    #region StopStreamAsync Tests

    [Fact]
    public async Task StopStreamAsync_DoesNotThrow_WhenNoActiveStream()
    {
        // Arrange
        var userId = 1;

        var handler = new MockHttpMessageHandler();
        var ttsService = CreateTtsService(handler);

        // Act & Assert - should not throw when no stream is active
        await ttsService.StopStreamAsync(userId);
    }

    #endregion
}

/// <summary>
/// A mock HttpMessageHandler that allows configurable responses for testing HTTP calls.
/// This is the standard .NET pattern for testing code that uses HttpClient,
/// since HttpClient methods like PostAsync/GetAsync are non-virtual and cannot be mocked.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _sendAsyncFunc;

    public MockHttpMessageHandler()
    {
        _sendAsyncFunc = null;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsyncFunc)
    {
        _sendAsyncFunc = sendAsyncFunc;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_sendAsyncFunc != null)
        {
            return await _sendAsyncFunc(request, cancellationToken);
        }

        // Default: return 200 OK with empty content
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("{}")
        };
    }
}
