using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealtimeTranscribe.Models;
using RealtimeTranscribe.Services;
using System.Collections.ObjectModel;
#if MACCATALYST
using AVFoundation;
#endif

namespace RealtimeTranscribe.ViewModels;

/// <summary>
/// Represents a single audio device in the device-picker list.
/// Carries the underlying <see cref="AudioDevice"/> data and exposes an observable
/// <see cref="IsSelected"/> flag so the UI can show a checkmark on the active item.
/// </summary>
public partial class SelectableAudioDevice : ObservableObject
{
    public AudioDevice Device { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SelectableAudioDevice(AudioDevice device, bool isSelected)
    {
        Device = device;
        _isSelected = isSelected;
    }
}

/// <summary>
/// ViewModel for <see cref="DevicesPage"/>.
/// Exposes the list of available input devices and the currently selected device,
/// and the command to change the selection.
/// <para>
/// When the user selects a different input device, <see cref="IAudioService.SetSelectedInputDevice"/>
/// is called, which raises <see cref="IAudioService.DeviceSelectionChanged"/> so that
/// <c>MainViewModel</c> can stop and restart any in-progress recording on the newly-selected
/// device.
/// </para>
/// <para>
/// There is intentionally no Output device picker.  Selecting an output device has no effect
/// on what gets recorded — recording always reads from the input device.  To capture audio
/// playing through an output device (e.g. AirPods), select the synthetic
/// "System Audio (all apps)" entry that appears at the top of the input list on macOS 14.2+.
/// </para>
/// </summary>
public partial class DevicesViewModel : ObservableObject
{
    private readonly IAudioService _audioService;

    [ObservableProperty]
    private ObservableCollection<SelectableAudioDevice> _inputDevices = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public DevicesViewModel(IAudioService audioService)
    {
        _audioService = audioService;
        LoadDevices();
    }

    /// <summary>
    /// Requests microphone permission (shows the OS dialog the first time) and then
    /// refreshes the input device list from the platform audio session.
    /// Called automatically when the page appears and when the user taps "Refresh".
    /// </summary>
    [RelayCommand]
    public async Task RefreshDevices()
    {
#if MACCATALYST
        // On macOS Catalyst, Permissions.RequestAsync<Microphone>() only sets the TCC record
        // via AVCaptureDevice.RequestAccessForMediaType.  It does NOT activate AVAudioSession
        // for recording, which means AVAudioSession.AvailableInputs remains null and CoreAudio
        // HAL input-scope stream queries still return nothing in the same process session.
        // AVAudioSession.RequestRecordPermission both requests the TCC microphone permission
        // AND registers the intent with the audio daemon so that the session is recording-ready
        // before GetInputDevices() is called.
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        AVAudioSession.SharedInstance().RequestRecordPermission(granted => tcs.TrySetResult(granted));
        await tcs.Task;
#else
        await Permissions.RequestAsync<Permissions.Microphone>();
#endif
        LoadDevices();
    }

    /// <summary>Selects <paramref name="device"/> as the active input device.</summary>
    [RelayCommand]
    private void SelectInputDevice(SelectableAudioDevice device)
    {
        if (device.IsSelected)
            return;

        foreach (var item in InputDevices)
            item.IsSelected = item.Device.Id == device.Device.Id;

        _audioService.SetSelectedInputDevice(device.Device.Id);
        StatusMessage = $"Input: {device.Device.Name}";
    }

    private void LoadDevices()
    {
        InputDevices = new ObservableCollection<SelectableAudioDevice>(
            BuildSelectableList(_audioService.GetInputDevices(), _audioService.SelectedInputDeviceId));

        StatusMessage = string.Empty;
    }

    /// <summary>
    /// Converts a flat device list into selectable items.
    /// When <paramref name="selectedId"/> is <see langword="null"/> or doesn't match any
    /// device, the first item is marked as selected (represents the OS default).
    /// </summary>
    private static IEnumerable<SelectableAudioDevice> BuildSelectableList(
        IReadOnlyList<AudioDevice> devices, string? selectedId)
    {
        if (devices.Count == 0)
            return [];

        var list = devices
            .Select(d => new SelectableAudioDevice(d, d.Id == selectedId))
            .ToList();

        // If nothing matched (e.g. previously-selected device is no longer available),
        // fall back to marking the first item as the selected default.
        if (!list.Any(d => d.IsSelected))
            list[0].IsSelected = true;

        return list;
    }
}
