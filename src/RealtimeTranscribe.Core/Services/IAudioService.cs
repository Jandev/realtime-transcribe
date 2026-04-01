using RealtimeTranscribe.Models;

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

    /// <summary>
    /// Raised when the user changes the selected input or output device.
    /// Subscribers (e.g. <c>MainViewModel</c>) stop any in-progress recording, apply the
    /// new device (buffering any audio that was captured so far into the transcript), and
    /// immediately restart recording on the newly-selected device so the session continues
    /// without clearing the transcript.
    /// </summary>
    event EventHandler? DeviceSelectionChanged;

    // ------------------------------------------------------------------
    // Device enumeration
    // ------------------------------------------------------------------

    /// <summary>Returns all available audio input devices (e.g. microphones).</summary>
    IReadOnlyList<AudioDevice> GetInputDevices();

    /// <summary>Returns all available audio output devices (e.g. speakers, headphones).</summary>
    IReadOnlyList<AudioDevice> GetOutputDevices();

    // ------------------------------------------------------------------
    // Device selection
    // ------------------------------------------------------------------

    /// <summary>
    /// The ID of the currently selected input device, or <see langword="null"/> to use the
    /// system default.
    /// </summary>
    string? SelectedInputDeviceId { get; }

    /// <summary>
    /// The ID of the currently selected output device, or <see langword="null"/> to use the
    /// system default.
    /// </summary>
    string? SelectedOutputDeviceId { get; }

    /// <summary>
    /// Selects the input device with the given <paramref name="deviceId"/>. Pass
    /// <see langword="null"/> to revert to the system default.
    /// Raises <see cref="DeviceSelectionChanged"/> after applying the change.
    /// </summary>
    void SetSelectedInputDevice(string? deviceId);

    /// <summary>
    /// Selects the output device with the given <paramref name="deviceId"/>. Pass
    /// <see langword="null"/> to revert to the system default.
    /// Raises <see cref="DeviceSelectionChanged"/> after applying the change.
    /// </summary>
    void SetSelectedOutputDevice(string? deviceId);

    // ------------------------------------------------------------------
    // Recording
    // ------------------------------------------------------------------

    /// <summary>Starts capturing audio from the selected (or default) input device.</summary>
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
