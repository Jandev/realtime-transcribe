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
    /// Raised when the user changes the selected input device.
    /// Subscribers (e.g. <c>MainViewModel</c>) stop any in-progress recording, apply the
    /// new device (buffering any audio that was captured so far into the transcript), and
    /// immediately restart recording on the newly-selected device so the session continues
    /// without clearing the transcript.
    /// </summary>
    event EventHandler? DeviceSelectionChanged;

    // ------------------------------------------------------------------
    // Device enumeration
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all available audio input sources.
    /// <para>
    /// On macOS 14.2+ this list includes a virtual "System Audio (all apps)" entry whose
    /// <see cref="AudioDevice.Id"/> equals <see cref="SystemAudioDeviceId"/>.  Selecting it
    /// captures the full system audio mix via the CoreAudio Process Tap API — it works
    /// regardless of which physical output device is in use, including AirPods and other
    /// Bluetooth headphones, without requiring BlackHole or other virtual loopback drivers.
    /// </para>
    /// </summary>
    IReadOnlyList<AudioDevice> GetInputDevices();

    // ------------------------------------------------------------------
    // Device selection
    // ------------------------------------------------------------------

    /// <summary>
    /// Well-known <see cref="AudioDevice.Id"/> for the synthetic "System Audio (all apps)"
    /// entry returned by <see cref="GetInputDevices"/>.  Pass this to
    /// <see cref="SetSelectedInputDevice"/> to record the system audio mix instead of a
    /// physical microphone.
    /// </summary>
    public const string SystemAudioDeviceId = "system-audio:loopback";

    /// <summary>
    /// The ID of the currently selected input device, or <see langword="null"/> to use the
    /// system default.
    /// </summary>
    string? SelectedInputDeviceId { get; }

    /// <summary>
    /// Selects the input device with the given <paramref name="deviceId"/>. Pass
    /// <see langword="null"/> to revert to the system default.
    /// Raises <see cref="DeviceSelectionChanged"/> after applying the change.
    /// </summary>
    void SetSelectedInputDevice(string? deviceId);

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
