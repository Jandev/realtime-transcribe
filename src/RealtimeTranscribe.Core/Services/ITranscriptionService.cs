namespace RealtimeTranscribe.Services;

/// <summary>
/// Provides speech-to-text transcription and meeting summarisation.
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Sends <paramref name="wavBytes"/> to the Whisper deployment and returns the transcript text.
    /// </summary>
    Task<string> TranscribeAsync(byte[] wavBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the GPT-4o deployment to produce a concise summary and action-item bullet list
    /// from <paramref name="transcript"/>.
    /// </summary>
    Task<string> SummarizeAsync(string transcript, CancellationToken cancellationToken = default);
}
