namespace RealtimeTranscribe.Models;

/// <summary>
/// Strongly-typed representation of the AzureOpenAI section in appsettings.json / user-secrets.
/// </summary>
public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string WhisperDeploymentName { get; set; } = "whisper-large-v3";
    public string ChatDeploymentName { get; set; } = "gpt-4o-mini";
}
