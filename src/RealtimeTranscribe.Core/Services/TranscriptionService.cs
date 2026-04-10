using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Audio;
using OpenAI.Chat;
using RealtimeTranscribe.Models;
using System.Runtime.CompilerServices;

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
        "Format your entire response using Markdown. " +
        "Always respond with: " +
        "1. A '## Summary' heading followed by a concise summary of no more than three sentences. " +
        "2. A '## Action Items' heading followed by a bullet-point list of action items / tasks identified in the meeting. " +
        "Use **bold** for key terms and *italic* for emphasis where appropriate.";

    public TranscriptionService(AzureOpenAISettings settings)
    {
        _settings = settings;
    }

    // A standard PCM WAV file has a 44-byte header (RIFF/fmt/data chunk descriptors) with
    // no audio sample data.  Any payload at or below this size contains no speech and must
    // not be sent to Whisper to avoid unnecessary API charges.
    private const int MinAudioDataBytes = 44;

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
    {
        if (wavBytes.Length <= MinAudioDataBytes)
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

    /// <inheritdoc/>
    public async Task SummarizeStreamingAsync(string transcript, Func<string, Task> onToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return;

        var client = BuildAzureClient();
        var chatClient = client.GetChatClient(_settings.ChatDeploymentName);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SummarisationSystemPrompt),
            new UserChatMessage($"Transcript:\n\n{transcript}")
        };

        var streaming = chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken);

        await foreach (var update in streaming.WithCancellation(cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    await onToken(part.Text);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public void WarmUp()
    {
        // Force Mono to create runtime vtables for the specific TaskAwaiter<T> instantiations
        // that appear in TranscribeAsync, DiarizeAsync, SummarizeAsync, and SummarizeStreamingAsync
        // state machines.  The generic type argument T is an Azure SDK response class; each distinct
        // T produces a separate MonoClass whose vtable must be initialised independently.
        //
        // Without this, the first Task.Run that JIT-compiles one of these methods on a TP Worker
        // hits interp_try_devirt → mono_class_get_virtual_method with a null vtable → EXC_BAD_ACCESS
        // at 0x00000000000000a0.
        //
        // RuntimeHelpers.RunClassConstructor is purely synchronous (no async IL), so calling it
        // cannot itself trigger the interp_try_devirt crash.

        // TranscribeAsync: await audioClient.TranscribeAudioAsync(…) → TaskAwaiter<ClientResult<AudioTranscription>>
        RuntimeHelpers.RunClassConstructor(
            typeof(TaskAwaiter<System.ClientModel.ClientResult<AudioTranscription>>).TypeHandle);

        // DiarizeAsync / SummarizeAsync: await chatClient.CompleteChatAsync(…) → TaskAwaiter<ClientResult<ChatCompletion>>
        RuntimeHelpers.RunClassConstructor(
            typeof(TaskAwaiter<System.ClientModel.ClientResult<ChatCompletion>>).TypeHandle);

        // Azure SDK internal methods use .ConfigureAwait(false) extensively.
        // Cover the ConfiguredTaskAwaiter variants for the same response types.
        RuntimeHelpers.RunClassConstructor(
            typeof(ConfiguredTaskAwaitable<System.ClientModel.ClientResult<AudioTranscription>>.ConfiguredTaskAwaiter).TypeHandle);
        RuntimeHelpers.RunClassConstructor(
            typeof(ConfiguredTaskAwaitable<System.ClientModel.ClientResult<ChatCompletion>>.ConfiguredTaskAwaiter).TypeHandle);

        // SummarizeStreamingAsync: CompleteChatStreamingAsync returns AsyncCollectionResult<StreamingChatCompletionUpdate>.
        // The await-foreach with .WithCancellation() uses ConfiguredCancelableAsyncEnumerable<T>.
        // Force-load the enumerator type so its vtable is ready.
        RuntimeHelpers.RunClassConstructor(
            typeof(System.Runtime.CompilerServices.ConfiguredCancelableAsyncEnumerable<StreamingChatCompletionUpdate>).TypeHandle);
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
