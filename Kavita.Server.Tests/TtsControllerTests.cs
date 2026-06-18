using Kavita.API.Services;
using Kavita.API.Store;
using Kavita.Models.DTOs.Tts;
using Kavita.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Kavita.Server.Tests.Controllers;

public class TtsControllerTests : IDisposable
{
    private readonly TtsController _controller;
    private readonly ITtsService _ttsService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserContext _userContext;

    public TtsControllerTests()
    {
        _ttsService = Substitute.For<ITtsService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _userContext = Substitute.For<IUserContext>();

        // Set up user context to return a valid UserId
        _userContext.GetUserIdOrThrow().Returns(1);
        _userContext.IsAuthenticated.Returns(true);

        // Create controller with mocked services
        _controller = new TtsController(_ttsService, _localizationService);

        // Set up HttpContext with IUserContext in the service provider
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IUserContext>(_userContext)
            .BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        // No unmanaged resources to dispose
    }

    #region GetTtsConfig Tests

    [Fact]
    public async Task GetTtsConfig_ReturnsOkResult_WithConfigDto()
    {
        // Arrange
        var expectedDto = new TtsServerConfigDto
        {
            ServerUrl = "https://api.openai.com",
            HasApiKey = true,
            DefaultModel = "tts-1",
            DefaultVoice = "alloy",
            DefaultSpeed = 1.0f
        };

        _ttsService.GetUserTtsConfigAsync(Arg.Any<int>())
            .Returns(expectedDto);

        // Act
        var actionResult = await _controller.GetTtsConfig();

        // Assert — ActionResult<T>.Result is OkObjectResult when Ok() is returned
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsAssignableFrom<TtsServerConfigDto>(okResult.Value);
        Assert.Equal(expectedDto.ServerUrl, dto.ServerUrl);
    }

    [Fact]
    public async Task GetTtsConfig_ReturnsOkResult_WithEmptyConfig_WhenNoneExists()
    {
        // Arrange
        var emptyDto = new TtsServerConfigDto();

        _ttsService.GetUserTtsConfigAsync(Arg.Any<int>())
            .Returns(emptyDto);

        // Act
        var actionResult = await _controller.GetTtsConfig();

        // Assert — ActionResult<T>.Result is OkObjectResult when Ok() is returned
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsAssignableFrom<TtsServerConfigDto>(okResult.Value);
        Assert.Equal(string.Empty, dto.ServerUrl);
    }

    #endregion

    #region SaveTtsConfig Tests

    [Fact]
    public async Task SaveTtsConfig_ReturnsOkResult_WhenValid()
    {
        // Arrange
        var request = new TtsSaveConfigRequest
        {
            ServerUrl = "https://api.openai.com",
            ApiKey = "test-key",
            DefaultModel = "tts-1-hd",
            DefaultVoice = "nova",
            DefaultSpeed = 0.9f
        };

        _localizationService.TranslateAsync(Arg.Any<int>(), Arg.Any<string>())
            .Returns("Configuration saved successfully");

        // Act
        var result = await _controller.SaveTtsConfig(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact(Skip = "ModelState validation is handled by framework before action execution, not testable in unit test")]
    public async Task SaveTtsConfig_ReturnsBadRequest_WhenInvalidModelState()
    {
        // Arrange
        var request = new TtsSaveConfigRequest
        {
            ServerUrl = string.Empty, // Invalid - required field empty
            DefaultModel = "tts-1",
            DefaultVoice = "alloy"
        };

        _controller.ModelState.AddModelError("ServerUrl", "Required");

        // Act
        var result = await _controller.SaveTtsConfig(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SaveTtsConfig_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        // Arrange
        var request = new TtsSaveConfigRequest
        {
            ServerUrl = string.Empty,
            DefaultModel = "tts-1",
            DefaultVoice = "alloy"
        };

        _ttsService.SaveUserTtsConfigAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(Task.FromException(new ArgumentException("Server URL is required.")));

        // Act
        var result = await _controller.SaveTtsConfig(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region TestTtsServer Tests

    [Fact]
    public async Task TestTtsServer_ReturnsOkResult_WithResponse()
    {
        // Arrange
        var request = new TtsTestRequestDto
        {
            ServerUrl = "https://api.openai.com",
            ApiKey = "test-key",
            Model = "tts-1",
            Voice = "alloy"
        };

        var expectedResponse = new TtsTestResponseDto
        {
            Success = true,
            Message = "Successfully connected and received valid audio."
        };

        _ttsService.TestTtsServerAsync(request)
            .Returns(expectedResponse);

        // Act
        var actionResult = await _controller.TestTtsServer(request);

        // Assert — ActionResult<T>.Result is OkObjectResult when Ok() is returned
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsAssignableFrom<TtsTestResponseDto>(okResult.Value);
        Assert.True(response.Success);
    }

    #endregion

    #region GetVoices Tests

    [Fact]
    public async Task GetVoices_ReturnsOkResult_WithVoiceList()
    {
        // Arrange
        var expectedVoices = new List<TtsVoiceDto>
        {
            new TtsVoiceDto { VoiceId = "alloy", Name = "Alloy" },
            new TtsVoiceDto { VoiceId = "nova", Name = "Nova" }
        };

        _ttsService.GetVoicesAsync(Arg.Any<int>())
            .Returns(expectedVoices);

        // Act
        var actionResult = await _controller.GetVoices();

        // Assert — ActionResult<T>.Result is OkObjectResult when Ok() is returned
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var voices = Assert.IsAssignableFrom<IList<TtsVoiceDto>>(okResult.Value);
        Assert.Equal(2, voices.Count);
    }

    #endregion

    #region GetTextChunks Tests

    [Fact]
    public async Task GetTextChunks_ReturnsOkResult_WithChunks()
    {
        // Arrange
        var chapterId = 1;
        var expectedResponse = new TextChunksResponseDto
        {
            Chunks = new List<TextChunkDto>
            {
                new TextChunkDto { Index = 0, Text = "Hello world" },
                new TextChunkDto { Index = 1, Text = "Second paragraph." }
            }
        };

        _ttsService.GetTextChunksAsync(chapterId, Arg.Any<TtsChunkGranularity>())
            .Returns(expectedResponse);

        // Act
        var actionResult = await _controller.GetTextChunks(chapterId, TtsChunkGranularity.Paragraph);

        // Assert — ActionResult<T>.Result is OkObjectResult when Ok() is returned
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsAssignableFrom<TextChunksResponseDto>(okResult.Value);
        Assert.Equal(2, response.Chunks.Count);
    }

    #endregion

    #region StartStream Tests

    [Fact]
    public async Task StartStream_ReturnsOkResult_WhenSuccessful()
    {
        // Arrange
        var chapterId = 1;
        var request = new TtsStreamRequest
        {
            StartChunk = 0,
            EndChunk = 5,
            Granularity = TtsChunkGranularity.Paragraph
        };

        _localizationService.TranslateAsync(Arg.Any<int>(), Arg.Any<string>())
            .Returns("Stream started");

        // Act
        var result = await _controller.StartStream(chapterId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task StartStream_ReturnsBadRequest_WhenInvalidOperationException()
    {
        // Arrange
        var chapterId = 1;
        var request = new TtsStreamRequest();

        _ttsService.StartStreamAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TtsStreamRequest>())
            .Returns(Task.FromException(new InvalidOperationException("TTS server not configured")));

        // Act
        var result = await _controller.StartStream(chapterId, request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StartStream_ReturnsBadRequest_WhenArgumentOutOfRangeException()
    {
        // Arrange
        var chapterId = 1;
        var request = new TtsStreamRequest();

        _ttsService.StartStreamAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TtsStreamRequest>())
            .Returns(Task.FromException(new ArgumentOutOfRangeException("Invalid chunk range")));

        // Act
        var result = await _controller.StartStream(chapterId, request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region StopStream Tests

    [Fact]
    public async Task StopStream_ReturnsOkResult()
    {
        // Arrange
        _localizationService.TranslateAsync(Arg.Any<int>(), Arg.Any<string>())
            .Returns("Stream stopped");

        // Act
        var result = await _controller.StopStream();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion
}
