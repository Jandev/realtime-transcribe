using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Tests the <see cref="IAudioService"/> contract, including the <see cref="IAudioService.RecordingInterrupted"/>
/// event that is used to signal Bluetooth / wireless device disconnection during an active recording.
/// </summary>
public class AudioServiceContractTests
{
    // ---------------------------------------------------------------------------
    // Minimal fake implementation that lets tests control IsRecording and fire the
    // RecordingInterrupted event without any platform dependencies.
    // ---------------------------------------------------------------------------
    private sealed class FakeAudioService : IAudioService
    {
        public bool IsRecording { get; set; }

        public event EventHandler? RecordingInterrupted;

        public Task StartRecordingAsync()
        {
            IsRecording = true;
            return Task.CompletedTask;
        }

        public Task<byte[]> StopRecordingAsync()
        {
            IsRecording = false;
            return Task.FromResult(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // minimal WAV header
        }

        public Task<byte[]> GetCurrentChunkAsync()
        {
            // Return a minimal audio chunk without changing IsRecording.
            return Task.FromResult(new byte[] { 0x52, 0x49, 0x46, 0x46 });
        }

        /// <summary>Simulates a Bluetooth device disconnecting mid-recording.</summary>
        public void SimulateDeviceDisconnect() =>
            RecordingInterrupted?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void RecordingInterrupted_IsExposedByInterface()
    {
        // Verifies that IAudioService declares the RecordingInterrupted event so that
        // callers relying on the interface (e.g. MainViewModel) can always subscribe.
        IAudioService service = new FakeAudioService();

        var raised = false;
        service.RecordingInterrupted += (_, _) => raised = true;

        ((FakeAudioService)service).SimulateDeviceDisconnect();

        Assert.True(raised);
    }

    [Fact]
    public void RecordingInterrupted_WithNoSubscribers_DoesNotThrow()
    {
        // Ensures that firing the event when nobody is subscribed is safe.
        var service = new FakeAudioService();

        var exception = Record.Exception(service.SimulateDeviceDisconnect);

        Assert.Null(exception);
    }

    [Fact]
    public void RecordingInterrupted_WithMultipleSubscribers_RaisesForAll()
    {
        var service = new FakeAudioService();
        var count = 0;

        service.RecordingInterrupted += (_, _) => count++;
        service.RecordingInterrupted += (_, _) => count++;

        service.SimulateDeviceDisconnect();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task StopRecordingAsync_WhenCalledAfterDeviceDisconnect_ReturnsAvailableAudio()
    {
        // Confirms that StopRecordingAsync can still return audio even after the device
        // has signalled an interruption (the service does not clear its buffer on interrupt).
        var service = new FakeAudioService();
        await service.StartRecordingAsync();

        service.SimulateDeviceDisconnect(); // device goes away

        var bytes = await service.StopRecordingAsync();

        Assert.NotEmpty(bytes);
    }
}
