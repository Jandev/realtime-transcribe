namespace RealtimeTranscribe.Models;

/// <summary>
/// Strongly-typed representation of the AzureOpenAI section in appsettings.json / user-secrets.
/// </summary>
/// <remarks>
/// Supports the following endpoint formats:
/// <list type="bullet">
///   <item>Azure OpenAI: <c>https://&lt;resource&gt;.openai.azure.com/</c></item>
///   <item>Azure AI Services (Cognitive Services): <c>https://&lt;resource&gt;.cognitiveservices.azure.com/</c></item>
///   <item>Azure AI Foundry: <c>https://&lt;project&gt;.services.ai.azure.com/api/projects/&lt;project&gt;</c></item>
/// </list>
/// </remarks>
public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Whisper deployment name. Default matches the Azure AI Foundry model name.</summary>
    public string WhisperDeploymentName { get; set; } = "whisper";

    /// <summary>Chat deployment name. Default matches the Azure AI Foundry model name.</summary>
    public string ChatDeploymentName { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Returns <see langword="true"/> when the configured endpoint points to an Azure AI Foundry
    /// project (<c>services.ai.azure.com</c>), rather than a classic Azure OpenAI resource or
    /// an Azure AI Services (Cognitive Services) resource.
    /// </summary>
    public bool IsAiFoundryEndpoint =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        Endpoint.Contains("services.ai.azure.com", StringComparison.OrdinalIgnoreCase);
}
