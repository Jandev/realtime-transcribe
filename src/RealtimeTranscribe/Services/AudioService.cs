using Plugin.Maui.Audio;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Wraps <see cref="IAudioRecorder"/> from Plugin.Maui.Audio to record microphone input
/// and return the captured audio as WAV bytes.
/// </summary>
public class AudioService : IAudioService
{
    private readonly IAudioManager _audioManager;
    private IAudioRecorder? _recorder;

    public AudioService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public bool IsRecording => _recorder?.IsRecording ?? false;

    public async Task StartRecordingAsync()
    {
        _recorder = _audioManager.CreateRecorder();
        await _recorder.StartAsync();
    }

    public async Task<byte[]> StopRecordingAsync()
    {
        if (_recorder is null || !_recorder.IsRecording)
            return Array.Empty<byte>();

        var audioSource = await _recorder.StopAsync();

        using var stream = audioSource.GetAudioStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GetCurrentChunkAsync()
    {
        if (_recorder is null || !_recorder.IsRecording)
            return Array.Empty<byte>();

        // Stop the current recording to capture the audio so far.
        var audioSource = await _recorder.StopAsync();

        // Immediately start a fresh recording segment.
        _recorder = _audioManager.CreateRecorder();
        await _recorder.StartAsync();

        using var stream = audioSource.GetAudioStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
