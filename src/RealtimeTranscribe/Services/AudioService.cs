using Plugin.Maui.Audio;
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
/// </summary>
public sealed class AudioService : IAudioService, IDisposable
{
    private readonly IAudioManager _audioManager;
    private IAudioRecorder? _recorder;

#if MACCATALYST || IOS
    private IDisposable? _routeChangeToken;
#endif

    public event EventHandler? RecordingInterrupted;

    public AudioService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
        SubscribeToAudioRouteChanges();
    }

    public bool IsRecording => _recorder?.IsRecording ?? false;

    public async Task StartRecordingAsync()
    {
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

    public void Dispose()
    {
#if MACCATALYST || IOS
        _routeChangeToken?.Dispose();
        _routeChangeToken = null;
#endif
        GC.SuppressFinalize(this);
    }
}
