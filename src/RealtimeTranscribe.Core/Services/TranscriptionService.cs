using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Audio;
using OpenAI.Chat;
using RealtimeTranscribe.Models;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Uses Azure AI services for transcription and summarisation.
/// </summary>
/// <remarks>
/// Works with both Azure OpenAI and Azure AI Foundry endpoints:
/// <list type="bullet">
///   <item>
///     <term>Azure OpenAI</term>
///     <description>Set <see cref="AzureOpenAISettings.Endpoint"/> to
///     <c>https://&lt;resource&gt;.openai.azure.com/</c></description>
///   </item>
///   <item>
///     <term>Azure AI Foundry</term>
///     <description>Set <see cref="AzureOpenAISettings.Endpoint"/> to the project endpoint
///     <c>https://&lt;project&gt;.services.ai.azure.com/api/projects/&lt;project&gt;</c>.
///     The <see cref="AzureOpenAIClient"/> appends
///     <c>/openai/deployments/{model}/…</c> to whatever base URL is configured, so the
///     Foundry project endpoint works without any extra plumbing.</description>
///   </item>
/// </list>
/// For keyless (Entra ID) authentication leave <see cref="AzureOpenAISettings.ApiKey"/> empty;
/// <see cref="Azure.AI.Projects.AIProjectClient"/> and <see cref="DefaultAzureCredential"/>
/// are used automatically in that case.
/// </remarks>
public class TranscriptionService : ITranscriptionService
{
    private readonly AzureOpenAISettings _settings;

    private const string DiarizationSystemPrompt =
        "You are a speaker diarization assistant. " +
        "Given a meeting transcript, identify the distinct speakers and label each turn. " +
        "Use the format \"Speaker N: text\" (where N starts at 1) for every turn. " +
        "The transcript may be in Dutch or English — do NOT translate; keep the original language. " +
        "If only one speaker is present, label all text as \"Speaker 1:\". " +
        "Return ONLY the diarized transcript with no additional commentary or explanation.";

    private const string SummarisationSystemPrompt =
        "You are an assistant that analyses meeting transcripts. " +
        "The transcript may be in Dutch or English. " +
        "Respond in the same language as the transcript. " +
        "Always respond with: " +
        "1. A concise summary of no more than three sentences. " +
        "2. A bullet-point list of action items / tasks identified in the meeting.";

    public TranscriptionService(AzureOpenAISettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
    {
        if (wavBytes.Length == 0)
            return string.Empty;

        var client = BuildAzureClient();
        var audioClient = client.GetAudioClient(_settings.WhisperDeploymentName);

        using var audioStream = new MemoryStream(wavBytes);
        var result = await audioClient.TranscribeAudioAsync(audioStream, "audio.wav", cancellationToken: cancellationToken);

        return result.Value.Text;
    }

    /// <inheritdoc/>
    public async Task<string> DiarizeAsync(string transcript, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return string.Empty;

        var client = BuildAzureClient();
        var chatClient = client.GetChatClient(_settings.ChatDeploymentName);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(DiarizationSystemPrompt),
            new UserChatMessage($"Transcript:\n\n{transcript}")
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return response.Value.Content[0].Text;
    }

    /// <inheritdoc/>
    public async Task<string> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return string.Empty;

        var client = BuildAzureClient();
        var chatClient = client.GetChatClient(_settings.ChatDeploymentName);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SummarisationSystemPrompt),
            new UserChatMessage($"Transcript:\n\n{transcript}")
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return response.Value.Content[0].Text;
    }

    private AzureOpenAIClient BuildAzureClient()
    {
        var endpoint = new Uri(_settings.Endpoint);

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new AzureOpenAIClient(endpoint, new Azure.AzureKeyCredential(_settings.ApiKey));
        }

        var credential = new DefaultAzureCredential();

        // For Azure AI Foundry endpoints, try to resolve the underlying OpenAI service
        // URL through the AIProjectClient connection registry (used when an Azure OpenAI
        // resource is connected to the Foundry project).
        // For native Foundry model deployments, AzureOpenAIClient works directly because
        // it appends /openai/deployments/{model}/… to whatever base URL is configured.
        if (_settings.IsAiFoundryEndpoint)
        {
            try
            {
                var projectClient = new AIProjectClient(endpoint, credential);
                var connection = projectClient.GetConnection(typeof(AzureOpenAIClient).FullName!);
                if (connection.TryGetLocatorAsUri(out Uri? uri) && uri is not null)
                {
                    return new AzureOpenAIClient(new Uri($"https://{uri.Host}"), credential);
                }
            }
            catch (Exception)
            {
                // No connected Azure OpenAI resource found – fall through and use the
                // project endpoint directly (native Foundry model deployment).
            }
        }

        return new AzureOpenAIClient(endpoint, credential);
    }
}
