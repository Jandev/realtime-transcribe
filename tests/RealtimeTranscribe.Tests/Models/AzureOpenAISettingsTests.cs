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
    public void Settings_CanBeConfiguredWithApiKey()
    {
        var settings = new AzureOpenAISettings
        {
            Endpoint = "https://myproject.services.ai.azure.com/api/projects/myproject",
            ApiKey = "sk-test-key"
        };

        Assert.False(string.IsNullOrEmpty(settings.ApiKey));
    }
}
