using RealtimeTranscribe.Models;
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
        public event EventHandler? DeviceSelectionChanged;

        private string? _selectedInputDeviceId;

        private readonly List<AudioDevice> _inputDevices = [];

        public string? SelectedInputDeviceId => _selectedInputDeviceId;

        public IReadOnlyList<AudioDevice> GetInputDevices() => _inputDevices;

        public void SetSelectedInputDevice(string? deviceId)
        {
            _selectedInputDeviceId = deviceId;
            DeviceSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddInputDevice(AudioDevice device) => _inputDevices.Add(device);

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

    // ---------------------------------------------------------------------------
    // Device selection contract tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetInputDevices_ReturnsRegisteredDevices()
    {
        var service = new FakeAudioService();
        service.AddInputDevice(new AudioDevice("mic-1", "Built-in Microphone"));
        service.AddInputDevice(new AudioDevice("mic-2", "USB Headset"));

        var devices = service.GetInputDevices();

        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, d => d.Id == "mic-1" && d.Name == "Built-in Microphone");
        Assert.Contains(devices, d => d.Id == "mic-2" && d.Name == "USB Headset");
    }

    [Fact]
    public void SetSelectedInputDevice_UpdatesSelectedInputDeviceId()
    {
        var service = new FakeAudioService();
        service.AddInputDevice(new AudioDevice("mic-1", "Built-in Microphone"));

        service.SetSelectedInputDevice("mic-1");

        Assert.Equal("mic-1", service.SelectedInputDeviceId);
    }

    [Fact]
    public void SetSelectedInputDevice_RaisesDeviceSelectionChanged()
    {
        var service = new FakeAudioService();
        var raised = false;
        service.DeviceSelectionChanged += (_, _) => raised = true;

        service.SetSelectedInputDevice("mic-1");

        Assert.True(raised);
    }

    [Fact]
    public void SetSelectedInputDevice_WithNull_ClearsSelection()
    {
        var service = new FakeAudioService();
        service.SetSelectedInputDevice("mic-1");

        service.SetSelectedInputDevice(null);

        Assert.Null(service.SelectedInputDeviceId);
    }

    [Fact]
    public void DeviceSelectionChanged_WithNoSubscribers_DoesNotThrow()
    {
        var service = new FakeAudioService();

        var exception = Record.Exception(() => service.SetSelectedInputDevice("mic-1"));

        Assert.Null(exception);
    }
}
