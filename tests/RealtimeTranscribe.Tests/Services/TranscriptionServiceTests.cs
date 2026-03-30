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
}

