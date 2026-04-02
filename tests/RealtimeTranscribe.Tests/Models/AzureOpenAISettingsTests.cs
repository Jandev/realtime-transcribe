using RealtimeTranscribe.Models;
using Xunit;

namespace RealtimeTranscribe.Tests.Models;

/// <summary>
/// Unit tests for <see cref="AzureOpenAISettings"/>.
/// </summary>
public class AzureOpenAISettingsTests
{
    [Fact]
    public void DefaultWhisperDeploymentName_IsWhisper()
    {
        var settings = new AzureOpenAISettings();

        Assert.Equal("whisper", settings.WhisperDeploymentName);
    }

    [Fact]
    public void DefaultChatDeploymentName_IsGpt4oMini()
    {
        var settings = new AzureOpenAISettings();

        Assert.Equal("gpt-4o-mini", settings.ChatDeploymentName);
    }

    [Theory]
    [InlineData("https://myproject.services.ai.azure.com/api/projects/myproject")]
    [InlineData("https://MYPROJECT.SERVICES.AI.AZURE.COM/api/projects/myproject")]
    public void IsAiFoundryEndpoint_ReturnsTrue_ForFoundryUrls(string endpoint)
    {
        var settings = new AzureOpenAISettings { Endpoint = endpoint };

        Assert.True(settings.IsAiFoundryEndpoint);
    }

    [Theory]
    [InlineData("https://myresource.openai.azure.com/")]
    [InlineData("https://myresource.openai.azure.com")]
    [InlineData("https://myresource.cognitiveservices.azure.com/")]
    [InlineData("https://myresource.cognitiveservices.azure.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAiFoundryEndpoint_ReturnsFalse_ForNonFoundryUrls(string endpoint)
    {
        var settings = new AzureOpenAISettings { Endpoint = endpoint };

        Assert.False(settings.IsAiFoundryEndpoint);
    }

    [Fact]
    public void Settings_CanBeConfiguredWithAiFoundryValues()
    {
        var settings = new AzureOpenAISettings
        {
            Endpoint = "https://myproject.services.ai.azure.com/api/projects/myproject",
            ApiKey = string.Empty,
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini"
        };

        Assert.True(settings.IsAiFoundryEndpoint);
        Assert.Equal("whisper", settings.WhisperDeploymentName);
        Assert.Equal("gpt-4o-mini", settings.ChatDeploymentName);
        Assert.True(string.IsNullOrEmpty(settings.ApiKey));
    }

    [Fact]
    public void Settings_CanBeConfiguredWithAiFoundryValuesAndApiKey()
    {
        // AI Foundry endpoint with an API key – both should be recognised so the service
        // can attempt connection resolution via AIProjectClient before falling back to the
        // project endpoint directly.
        var settings = new AzureOpenAISettings
        {
            Endpoint = "https://myproject.services.ai.azure.com/api/projects/myproject",
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini"
        };

        Assert.True(settings.IsAiFoundryEndpoint);
        Assert.Equal("whisper", settings.WhisperDeploymentName);
        Assert.Equal("gpt-4o-mini", settings.ChatDeploymentName);
        Assert.False(string.IsNullOrEmpty(settings.ApiKey));
    }

    [Fact]
    public void Settings_CanBeConfiguredWithCognitiveServicesValues()
    {
        // Azure AI Services (Cognitive Services) endpoint — the actual URL format used by the
        // service when models are accessed through a cognitiveservices.azure.com resource.
        // e.g. whisper at .../openai/deployments/whisper/audio/transcriptions
        //      gpt at    .../openai/deployments/gpt-4o-mini/chat/completions
        var settings = new AzureOpenAISettings
        {
            Endpoint = "https://myresource.cognitiveservices.azure.com",
            ApiKey = "test-api-key",
            WhisperDeploymentName = "whisper",
            ChatDeploymentName = "gpt-4o-mini"
        };

        Assert.False(settings.IsAiFoundryEndpoint);
        Assert.Equal("whisper", settings.WhisperDeploymentName);
        Assert.Equal("gpt-4o-mini", settings.ChatDeploymentName);
        Assert.False(string.IsNullOrEmpty(settings.ApiKey));
    }
}
