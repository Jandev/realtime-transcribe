namespace RealtimeTranscribe.Services;

/// <summary>
/// Schedules periodic audio capture and transcription while a recording is active.
/// </summary>
public interface ITranscriptionScheduler
{
    /// <summary>
    /// Runs a periodic loop that captures audio chunks and transcribes them at a
    /// configured interval until <paramref name="cancellationToken"/> is cancelled.
    /// Each non-empty transcribed segment is delivered to <paramref name="onSegment"/>.
    /// </summary>
    /// <param name="audioChunkProvider">
    /// Async delegate that captures the audio recorded since the last call and returns
    /// it as raw WAV bytes. Returns an empty array when no audio is available.
    /// </param>
    /// <param name="onSegment">
    /// Async callback invoked with each successfully transcribed segment text.
    /// </param>
    /// <param name="cancellationToken">Token that stops the scheduling loop.</param>
    Task RunAsync(
        Func<CancellationToken, Task<byte[]>> audioChunkProvider,
        Func<string, Task> onSegment,
        CancellationToken cancellationToken);
}
