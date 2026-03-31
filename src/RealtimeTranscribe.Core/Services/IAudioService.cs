namespace RealtimeTranscribe.Services;

/// <summary>
/// Abstraction over the platform audio recorder.
/// </summary>
public interface IAudioService
{
    bool IsRecording { get; }

    /// <summary>
    /// Raised when an active recording is interrupted by an external event such as a Bluetooth
    /// input device disconnecting. Subscribers should call <see cref="StopRecordingAsync"/> to
    /// retrieve any audio that was captured before the interruption.
    /// </summary>
    event EventHandler? RecordingInterrupted;

    /// <summary>Starts capturing audio from the default microphone.</summary>
    Task StartRecordingAsync();

    /// <summary>
    /// Stops capturing audio and returns the recorded audio as raw WAV bytes.
    /// Returns an empty array when no audio was captured.
    /// </summary>
    Task<byte[]> StopRecordingAsync();

    /// <summary>
    /// Captures the audio recorded since the last call (or since recording started),
    /// starts a fresh recording segment, and returns the captured audio as raw WAV bytes.
    /// Returns an empty array if no recording is in progress.
    /// </summary>
    Task<byte[]> GetCurrentChunkAsync();
}
