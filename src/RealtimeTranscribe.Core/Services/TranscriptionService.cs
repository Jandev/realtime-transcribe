using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Audio;
using OpenAI.Chat;
using RealtimeTranscribe.Models;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Uses the Azure OpenAI Whisper model for transcription and a GPT-4o model for summarisation.
/// </summary>
public class TranscriptionService : ITranscriptionService
{
    private readonly AzureOpenAISettings _settings;

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

        // Fall back to DefaultAzureCredential (managed identity / az login)
        return new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
    }
}
