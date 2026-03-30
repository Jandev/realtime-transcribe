namespace RealtimeTranscribe.Services;

/// <summary>
/// Implements <see cref="ITranscriptionScheduler"/> by waiting a fixed interval between
/// transcription requests, which keeps request cadence within the Whisper quota
/// (default target: one request every 30 seconds, well inside the 3 RPM limit).
/// </summary>
public class TranscriptionScheduler : ITranscriptionScheduler
{
    /// <summary>Default interval between transcription requests (30 seconds).</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);

    private readonly ITranscriptionService _transcriptionService;
    private readonly TimeSpan _interval;

    /// <param name="transcriptionService">Service used to transcribe audio chunks.</param>
    /// <param name="interval">
    /// Interval between successive transcription calls.
    /// Defaults to <see cref="DefaultInterval"/> when <see langword="null"/>.
    /// </param>
    public TranscriptionScheduler(ITranscriptionService transcriptionService, TimeSpan? interval = null)
    {
        _transcriptionService = transcriptionService;
        _interval = interval ?? DefaultInterval;
    }

    /// <inheritdoc/>
    public async Task RunAsync(
        Func<CancellationToken, Task<byte[]>> audioChunkProvider,
        Func<string, Task> onSegment,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait for the configured interval before capturing the next chunk.
            try
            {
                await Task.Delay(_interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            // Capture the audio chunk recorded during the interval.
            byte[] chunk;
            try
            {
                chunk = await audioChunkProvider(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // Skip this cycle on audio capture error; the next interval will retry.
                continue;
            }

            if (chunk.Length == 0)
                continue;

            // Transcribe the captured chunk.
            string segment;
            try
            {
                segment = await _transcriptionService.TranscribeAsync(chunk, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // Skip this cycle on transient API failure; the next interval will retry.
                continue;
            }

            if (!string.IsNullOrEmpty(segment))
                await onSegment(segment);
        }
    }
}
