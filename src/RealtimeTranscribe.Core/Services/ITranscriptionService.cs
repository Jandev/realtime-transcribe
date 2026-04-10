namespace RealtimeTranscribe.Services;

/// <summary>
/// Provides speech-to-text transcription, speaker diarization, and meeting summarisation.
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Sends <paramref name="wavBytes"/> to the Whisper deployment and returns the transcript text.
    /// </summary>
    Task<string> TranscribeAsync(byte[] wavBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the GPT deployment to analyse <paramref name="transcript"/> and return a
    /// speaker-attributed version formatted as <c>Speaker N: text</c> lines.
    /// </summary>
    /// <remarks>
    /// Speaker identification is performed through conversational-context analysis rather than
    /// audio analysis, so accuracy depends on how clearly speaker turns are distinguishable in
    /// the text. Confidence is lower for monologues or overlapping speech.
    /// Returns <see cref="string.Empty"/> when <paramref name="transcript"/> is empty or
    /// whitespace.
    /// </remarks>
    Task<string> DiarizeAsync(string transcript, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the GPT-4o deployment to produce a concise summary and action-item bullet list
    /// from <paramref name="transcript"/>.
    /// </summary>
    Task<string> SummarizeAsync(string transcript, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams summary tokens to <paramref name="onToken"/> as they arrive from the model,
    /// allowing the UI to update incrementally.
    /// </summary>
    /// <param name="transcript">The full meeting transcript to summarise.</param>
    /// <param name="onToken">Async callback invoked with each streamed token fragment.</param>
    /// <param name="cancellationToken">Token that cancels the streaming request.</param>
    Task SummarizeStreamingAsync(string transcript, Func<string, Task> onToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces Mono runtime vtable initialisation for Azure SDK response types used as
    /// generic type arguments in the awaiter structs of <see cref="TranscribeAsync"/>,
    /// <see cref="DiarizeAsync"/>, and <see cref="SummarizeStreamingAsync"/>.
    /// <para>
    /// Must be called on the main thread during startup — before any <see cref="Task.Run"/>
    /// worker can trigger first-time JIT compilation of those async state machines.
    /// </para>
    /// </summary>
    void WarmUp();
}
