namespace RealtimeTranscribe.Services;

/// <summary>
/// Abstraction over the platform audio recorder.
/// </summary>
public interface IAudioService
{
    bool IsRecording { get; }

    /// <summary>Starts capturing audio from the default microphone.</summary>
    Task StartRecordingAsync();

    /// <summary>
    /// Stops capturing audio and returns the recorded audio as raw WAV bytes.
    /// </summary>
    Task<byte[]> StopRecordingAsync();
}
