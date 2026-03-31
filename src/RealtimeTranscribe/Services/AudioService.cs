using Plugin.Maui.Audio;
using RealtimeTranscribe.Models;
#if MACCATALYST || IOS
using AVFoundation;
using Foundation;
#endif

namespace RealtimeTranscribe.Services;

/// <summary>
/// Wraps <see cref="IAudioRecorder"/> from Plugin.Maui.Audio to record microphone input
/// and return the captured audio as WAV bytes.
/// <para>
/// On MacCatalyst and iOS this service also subscribes to <c>AVAudioSession</c> route-change
/// notifications so that a Bluetooth device disconnecting mid-recording is handled gracefully:
/// it raises <see cref="RecordingInterrupted"/> instead of silently dropping the captured audio.
/// </para>
/// <para>
/// Device selection is persisted via <see cref="Preferences"/> and applied to
/// <c>AVAudioSession</c> before each recording segment starts.
/// </para>
/// </summary>
public sealed class AudioService : IAudioService, IDisposable
{
    private const string InputDevicePreferenceKey = "SelectedInputDeviceId";
    private const string OutputDevicePreferenceKey = "SelectedOutputDeviceId";

    private readonly IAudioManager _audioManager;
    private IAudioRecorder? _recorder;

    private string? _selectedInputDeviceId;
    private string? _selectedOutputDeviceId;

#if MACCATALYST || IOS
    private IDisposable? _routeChangeToken;
#endif

    public event EventHandler? RecordingInterrupted;
    public event EventHandler? DeviceSelectionChanged;

    public AudioService(IAudioManager audioManager)
    {
        _audioManager = audioManager;

        // Restore persisted device selections.
        var savedInput = Preferences.Default.Get(InputDevicePreferenceKey, string.Empty);
        _selectedInputDeviceId = string.IsNullOrEmpty(savedInput) ? null : savedInput;

        var savedOutput = Preferences.Default.Get(OutputDevicePreferenceKey, string.Empty);
        _selectedOutputDeviceId = string.IsNullOrEmpty(savedOutput) ? null : savedOutput;

        SubscribeToAudioRouteChanges();
    }

    public bool IsRecording => _recorder?.IsRecording ?? false;

    // ------------------------------------------------------------------
    // Device enumeration
    // ------------------------------------------------------------------

    public IReadOnlyList<AudioDevice> GetInputDevices()
    {
#if MACCATALYST || IOS
        var session = AVAudioSession.SharedInstance();
        var inputs = session.AvailableInputs;
        if (inputs is { Length: > 0 })
            return inputs.Select(p => new AudioDevice(p.Uid, p.PortName)).ToArray();
#endif
        return Array.Empty<AudioDevice>();
    }

    public IReadOnlyList<AudioDevice> GetOutputDevices()
    {
#if MACCATALYST || IOS
        var session = AVAudioSession.SharedInstance();
        var outputs = session.CurrentRoute?.Outputs;
        if (outputs is { Length: > 0 })
            return outputs.Select(p => new AudioDevice(p.Uid, p.PortName)).ToArray();
#endif
        return Array.Empty<AudioDevice>();
    }

    // ------------------------------------------------------------------
    // Device selection
    // ------------------------------------------------------------------

    public string? SelectedInputDeviceId => _selectedInputDeviceId;
    public string? SelectedOutputDeviceId => _selectedOutputDeviceId;

    public void SetSelectedInputDevice(string? deviceId)
    {
        _selectedInputDeviceId = deviceId;
        Preferences.Default.Set(InputDevicePreferenceKey, deviceId ?? string.Empty);
        DeviceSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSelectedOutputDevice(string? deviceId)
    {
        _selectedOutputDeviceId = deviceId;
        Preferences.Default.Set(OutputDevicePreferenceKey, deviceId ?? string.Empty);
        DeviceSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------
    // Recording
    // ------------------------------------------------------------------

    public async Task StartRecordingAsync()
    {
        ApplyPreferredInputDevice();
        _recorder = _audioManager.CreateRecorder();
        await _recorder.StartAsync();
    }

    public async Task<byte[]> StopRecordingAsync()
    {
        if (_recorder is null)
            return Array.Empty<byte>();

        try
        {
            // Always attempt StopAsync even when IsRecording is false: a Bluetooth device
            // may have disconnected and caused the underlying recorder to stop on its own,
            // but there may still be audio data buffered that we can retrieve.
            var audioSource = await _recorder.StopAsync();
            using var stream = audioSource.GetAudioStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            // Log the exception for diagnostics but return an empty array so the caller
            // can handle the "no audio" case gracefully without crashing the app.
            System.Diagnostics.Debug.WriteLine($"[AudioService] StopRecordingAsync failed: {ex}");
            return Array.Empty<byte>();
        }
    }

    public async Task<byte[]> GetCurrentChunkAsync()
    {
        if (_recorder is null || !_recorder.IsRecording)
            return Array.Empty<byte>();

        // Stop the current recording to capture the audio so far.
        var audioSource = await _recorder.StopAsync();

        // Immediately start a fresh recording segment using the selected device.
        ApplyPreferredInputDevice();
        _recorder = _audioManager.CreateRecorder();
        await _recorder.StartAsync();

        using var stream = audioSource.GetAudioStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    // ------------------------------------------------------------------
    // Route-change monitoring (Bluetooth / wireless device support)
    // ------------------------------------------------------------------

    private void SubscribeToAudioRouteChanges()
    {
#if MACCATALYST || IOS
        _routeChangeToken = AVAudioSession.Notifications.ObserveRouteChange((_, args) =>
        {
            // OldDeviceUnavailable fires when a Bluetooth headset / AirPods disconnect.
            if (IsRecording && args.Reason == AVAudioSessionRouteChangeReason.OldDeviceUnavailable)
            {
                RecordingInterrupted?.Invoke(this, EventArgs.Empty);
            }
        });
#endif
    }

    /// <summary>
    /// Applies the user's preferred input device to the <c>AVAudioSession</c> so the next
    /// recording segment uses it. Has no effect when no device is selected (system default is used).
    /// </summary>
    private void ApplyPreferredInputDevice()
    {
#if MACCATALYST || IOS
        if (_selectedInputDeviceId is null)
            return;

        var session = AVAudioSession.SharedInstance();
        var preferred = session.AvailableInputs?
            .FirstOrDefault(p => p.Uid == _selectedInputDeviceId);

        if (preferred is not null)
            session.SetPreferredInput(preferred, out _);
#endif
    }

    public void Dispose()
    {
#if MACCATALYST || IOS
        _routeChangeToken?.Dispose();
        _routeChangeToken = null;
#endif
        GC.SuppressFinalize(this);
    }
}
