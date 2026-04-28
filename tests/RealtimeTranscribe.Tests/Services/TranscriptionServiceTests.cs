using RealtimeTranscribe.Models;
using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TranscriptionService"/> guard clauses.
/// These tests exercise logic paths that return before any network call is made,
/// so no Azure credentials are required.
/// </summary>
public class TranscriptionServiceTests
{
    // Uses an Azure AI Foundry project endpoint to validate that the service
    // is configured for the AI Foundry URL format.
    private const string AiFoundryEndpoint =
        "https://fake-project.services.ai.azure.com/api/projects/fake-project";

    private static TranscriptionService CreateService() =>
        new(new AzureOpenAISettings
        {
            Endpoint = AiFoundryEndpoint,
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini"
        });

    [Fact]
    public async Task TranscribeAsync_WithEmptyByteArray_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.TranscribeAsync(Array.Empty<byte>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_WithWavHeaderOnlyBytes_ReturnsEmptyString()
    {
        // A standard PCM WAV header is 44 bytes.  A payload at or below that size
        // contains no audio samples and must not be forwarded to Whisper.
        var service = CreateService();
        var headerOnlyWav = new byte[44]; // exactly the header, no audio data

        var result = await service.TranscribeAsync(headerOnlyWav);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_WithBytesJustBelowThreshold_ReturnsEmptyString()
    {
        // Any payload smaller than the 44-byte WAV header is also invalid.
        var service = CreateService();
        var tooSmall = new byte[10];

        var result = await service.TranscribeAsync(tooSmall);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task DiarizeAsync_WithEmptyTranscript_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.DiarizeAsync(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task DiarizeAsync_WithWhitespaceTranscript_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.DiarizeAsync("   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SummarizeAsync_WithEmptyTranscript_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.SummarizeAsync(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SummarizeAsync_WithWhitespaceTranscript_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.SummarizeAsync("   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_WithApiKeySettings_ReturnsEmptyStringForEmptyInput()
    {
        // Verifies the API-key branch of BuildAzureClient still honours the guard clause.
        var service = new TranscriptionService(new AzureOpenAISettings
        {
            Endpoint = AiFoundryEndpoint,
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini"
        });

        var result = await service.TranscribeAsync(Array.Empty<byte>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_WithCognitiveServicesEndpoint_ReturnsEmptyStringForEmptyInput()
    {
        // Validates the common Azure AI Services (cognitiveservices.azure.com) endpoint format,
        // which is the same URL base used when models like 'whisper' and 'gpt-4o-mini' are
        // deployed to an Azure Cognitive Services resource.
        var service = new TranscriptionService(new AzureOpenAISettings
        {
            Endpoint = "https://fake.cognitiveservices.azure.com",
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini"
        });

        var result = await service.TranscribeAsync(Array.Empty<byte>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SummarizeAsync_WithCognitiveServicesEndpoint_ReturnsEmptyStringForEmptyInput()
    {
        var service = new TranscriptionService(new AzureOpenAISettings
        {
            Endpoint = "https://fake.cognitiveservices.azure.com",
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini"
        });

        var result = await service.SummarizeAsync(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_WithSystemPromptFilePathSet_StillReturnsEmptyForSmallInput()
    {
        // Guard clause must fire even when a system prompt file path is configured.
        // The file does not exist, so reading it will silently return empty — but the
        // size guard must still fire before any network call is attempted.
        var service = new TranscriptionService(new AzureOpenAISettings
        {
            Endpoint = AiFoundryEndpoint,
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini",
            SystemPromptFilePath = "/nonexistent/context.md"
        });
        var headerOnlyWav = new byte[44];

        var result = await service.TranscribeAsync(headerOnlyWav);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task DiarizeAsync_WithSystemPromptFilePathSet_StillReturnsEmptyForBlankTranscript()
    {
        // Guard clause must fire even when a system prompt file path is configured.
        var service = new TranscriptionService(new AzureOpenAISettings
        {
            Endpoint = AiFoundryEndpoint,
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini",
            SystemPromptFilePath = "/nonexistent/context.md"
        });

        var result = await service.DiarizeAsync(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SummarizeAsync_WithSystemPromptFilePathSet_StillReturnsEmptyForBlankTranscript()
    {
        // Guard clause must fire even when a system prompt file path is configured.
        var service = new TranscriptionService(new AzureOpenAISettings
        {
            Endpoint = AiFoundryEndpoint,
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini",
            SystemPromptFilePath = "/nonexistent/context.md"
        });

        var result = await service.SummarizeAsync(string.Empty);

        Assert.Equal(string.Empty, result);
    }
}
