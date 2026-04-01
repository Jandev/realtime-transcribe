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
/// Exposes the lists of available input and output devices, the currently selected device
/// in each list, and commands to change the selection.
/// <para>
/// When the user selects a different input or output device, <see cref="IAudioService.SetSelectedInputDevice"/>
/// / <see cref="IAudioService.SetSelectedOutputDevice"/> is called, which raises
/// <see cref="IAudioService.DeviceSelectionChanged"/> so that <c>MainViewModel</c> can
/// stop and restart any in-progress recording on the newly-selected device.
/// </para>
/// </summary>
public partial class DevicesViewModel : ObservableObject
{
    private readonly IAudioService _audioService;

    [ObservableProperty]
    private ObservableCollection<SelectableAudioDevice> _inputDevices = [];

    [ObservableProperty]
    private ObservableCollection<SelectableAudioDevice> _outputDevices = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public DevicesViewModel(IAudioService audioService)
    {
        _audioService = audioService;
        LoadDevices();
    }

    /// <summary>
    /// Requests microphone permission (shows the OS dialog the first time) and then
    /// refreshes both device lists from the platform audio session.
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

    /// <summary>Selects <paramref name="device"/> as the active output device.</summary>
    [RelayCommand]
    private void SelectOutputDevice(SelectableAudioDevice device)
    {
        if (device.IsSelected)
            return;

        foreach (var item in OutputDevices)
            item.IsSelected = item.Device.Id == device.Device.Id;

        _audioService.SetSelectedOutputDevice(device.Device.Id);
        StatusMessage = $"Output: {device.Device.Name}";
    }

    private void LoadDevices()
    {
        InputDevices = new ObservableCollection<SelectableAudioDevice>(
            BuildSelectableList(_audioService.GetInputDevices(), _audioService.SelectedInputDeviceId));

        OutputDevices = new ObservableCollection<SelectableAudioDevice>(
            BuildSelectableList(_audioService.GetOutputDevices(), _audioService.SelectedOutputDeviceId));

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
