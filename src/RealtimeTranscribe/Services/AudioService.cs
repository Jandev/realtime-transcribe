using Plugin.Maui.Audio;
using RealtimeTranscribe.Models;
#if MACCATALYST || IOS
using AVFoundation;
using Foundation;
#endif
#if MACCATALYST
using System.Runtime.InteropServices;
using CoreFoundation;
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

    // True for the entire duration of a recording session — from the moment StartRecordingAsync
    // is entered until StopRecordingAsync completes.  Unlike _recorder.IsRecording, this flag
    // stays true during the brief inter-recorder gaps inside GetCurrentChunkAsync() (where the
    // old recorder has stopped but the new one has not yet started).  GetInputDevices() uses
    // this flag so that SetCategory/SetActive is never called while the session is active,
    // preventing a spurious FinishedRecording callback that would otherwise crash the app.
    private volatile bool _isRecordingSession = false;

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
#if MACCATALYST
        var session = AVAudioSession.SharedInstance();
        if (!_isRecordingSession)
        {
            // Activate the audio session so the OS grants microphone access before we
            // query CoreAudio.  Without this, kAudioDevicePropertyStreams with input scope
            // returns no streams (permission has not yet been acknowledged by the OS) and
            // the device list is empty.  The PlayAndRecord category also ensures that
            // Bluetooth and Continuity devices are included in the HAL device list.
            //
            // IMPORTANT: Skip this when a recording session is active (including during the
            // brief inter-recorder gap in GetCurrentChunkAsync where the old recorder has
            // stopped but the new one has not yet started).  Calling SetCategory or
            // SetActive while AVAudioRecorder is running — or while we are restarting it —
            // can interrupt the active recording, causing AVAudioRecorder's FinishedRecording
            // delegate to fire prematurely.  When the user later stops recording our
            // StopAsync() call triggers FinishedRecording a second time, which calls
            // SetResult() on an already-completed TaskCompletionSource and crashes the app.
            session.SetCategory(AVAudioSessionCategory.PlayAndRecord,
                AVAudioSessionCategoryOptions.AllowBluetooth | AVAudioSessionCategoryOptions.AllowBluetoothA2DP,
                out _);
            session.SetActive(true, out _);
        }
        // Use the CoreAudio HAL (not AVAudioSession.AvailableInputs) because AvailableInputs
        // only returns the currently-active port and omits virtual/aggregate drivers such as
        // BlackHole.  The HAL is the authoritative source for ALL devices.
        var coreAudioDevices = GetCoreAudioDevices(inputScope: true);
        if (coreAudioDevices.Count > 0)
            return coreAudioDevices;

        // Fallback: the CoreAudio HAL can return nothing immediately after the user grants
        // microphone permission via the TCC dialog (the new grant has not yet propagated into
        // the current HAL session).  AVAudioSession.AvailableInputs reflects TCC grants
        // immediately via the high-level audio stack, so it reliably shows at least the
        // built-in microphone even when the HAL query returns zero results.
        var availableInputs = session.AvailableInputs;
        if (availableInputs is { Length: > 0 })
            return availableInputs.Select(p => new AudioDevice($"{p.PortType}:{p.PortName}", p.PortName)).ToArray();

        return Array.Empty<AudioDevice>();
#elif IOS
        // Set the session category to PlayAndRecord so that AVAudioSession exposes the
        // full set of available input ports: built-in mic, aggregated/virtual devices,
        // Bluetooth, and iPhone via Continuity.
        // Without this, only the currently-active port is returned.
        //
        // IMPORTANT: Skip this when a recording session is active to avoid interrupting the
        // active AVAudioRecorder or the inter-recorder restart gap in GetCurrentChunkAsync
        // (see MacCatalyst note above for the full explanation).
        var session = AVAudioSession.SharedInstance();
        if (!_isRecordingSession)
        {
            session.SetCategory(AVAudioSessionCategory.PlayAndRecord,
                AVAudioSessionCategoryOptions.AllowBluetooth | AVAudioSessionCategoryOptions.AllowBluetoothA2DP,
                out _);
            session.SetActive(true, out _);
        }
        var inputs = session.AvailableInputs;
        if (inputs is { Length: > 0 })
            return inputs.Select(p => new AudioDevice($"{p.PortType}:{p.PortName}", p.PortName)).ToArray();
#endif
        return Array.Empty<AudioDevice>();
    }

    public IReadOnlyList<AudioDevice> GetOutputDevices()
    {
#if MACCATALYST
        return GetCoreAudioDevices(inputScope: false);
#elif IOS
        var session = AVAudioSession.SharedInstance();
        var outputs = session.CurrentRoute?.Outputs;
        if (outputs is { Length: > 0 })
            return outputs.Select(p => new AudioDevice($"{p.PortType}:{p.PortName}", p.PortName)).ToArray();
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
        // Set the session flag before creating the recorder so that GetInputDevices()
        // never calls SetCategory/SetActive from the very start of the session.
        _isRecordingSession = true;
        try
        {
            ApplyPreferredInputDevice();
            _recorder = _audioManager.CreateRecorder();
            await _recorder.StartAsync();
        }
        catch
        {
            _isRecordingSession = false;
            throw;
        }
    }

    public async Task<byte[]> StopRecordingAsync()
    {
        if (_recorder is null)
            return Array.Empty<byte>();

        try
        {
            if (!_recorder.IsRecording)
            {
                // The recorder was already stopped by an external interruption (e.g. a phone
                // call, Siri, or another app taking over the audio session).  The underlying
                // AVAudioRecorder's FinishedRecording delegate has already fired and completed
                // the internal TaskCompletionSource.  Calling StopAsync() now would invoke
                // recorder.Stop() a second time, which can trigger FinishedRecording again
                // and call SetResult() on an already-completed TCS, throwing
                // InvalidOperationException inside a native ObjC callback — an unhandled
                // exception that crashes the app.  Return empty bytes so the caller can work
                // with whatever transcript segments were captured before the interruption.
                return Array.Empty<byte>();
            }

            // Always attempt StopAsync when the recorder is still active: a Bluetooth device
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
        finally
        {
            // Always clear the session flag so GetInputDevices() can reconfigure the audio
            // session again once the recording session has fully ended.
            _isRecordingSession = false;
        }
    }

    public async Task<byte[]> GetCurrentChunkAsync()
    {
        if (_recorder is null || !_recorder.IsRecording)
            return Array.Empty<byte>();

        // Stop the current recording to capture the audio so far.
        // Wrap in try/catch: if the recorder was interrupted between the IsRecording check
        // above and the StopAsync call (a very narrow race), StopAsync may throw.  We still
        // need to restart the recorder below so that the recording session continues.
        IAudioSource? audioSource = null;
        try
        {
            audioSource = await _recorder.StopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] GetCurrentChunkAsync StopAsync failed: {ex}");
        }

        // Always start a fresh recording segment, even if capturing the current chunk
        // failed, so that the session continues rather than silently going quiet.
        ApplyPreferredInputDevice();
        _recorder = _audioManager.CreateRecorder();
        await _recorder.StartAsync();

        if (audioSource is null)
            return Array.Empty<byte>();

        using var stream = audioSource.GetAudioStream();
        if (stream is null)
            return Array.Empty<byte>();

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
    /// Applies the user's preferred input device so the next recording segment uses it.
    /// <para>
    /// On MacCatalyst the device list is sourced from the CoreAudio HAL (which includes
    /// virtual/aggregate drivers such as BlackHole).  <c>AVAudioSession.setPreferredInput</c>
    /// only works with ports already in <c>AvailableInputs</c> — it cannot route to HAL
    /// devices that are absent from that list.  Instead we set the CoreAudio system-default
    /// input device directly so that <c>AVAudioRecorder</c> (used internally by
    /// Plugin.Maui.Audio) picks it up on the next recording start.
    /// </para>
    /// <para>
    /// On iOS, the standard <c>AVAudioSession.setPreferredInput</c> path is used.
    /// </para>
    /// </summary>
    private void ApplyPreferredInputDevice()
    {
#if MACCATALYST
        if (_selectedInputDeviceId is null)
            return;

        SetCoreAudioDefaultInputDevice(_selectedInputDeviceId);
#elif IOS
        if (_selectedInputDeviceId is null)
            return;

        var session = AVAudioSession.SharedInstance();
        var preferred = session.AvailableInputs?
            .FirstOrDefault(p => $"{p.PortType}:{p.PortName}" == _selectedInputDeviceId);

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

#if MACCATALYST
    // ------------------------------------------------------------------
    // CoreAudio P/Invoke helpers (macOS / MacCatalyst only)
    //
    // AVAudioSession.AvailableInputs on macOS Catalyst returns only the
    // active input port, and CurrentRoute.Outputs returns only the active
    // output.  The CoreAudio HAL (kAudioHardwarePropertyDevices) is the
    // authoritative source for ALL devices, including aggregated devices,
    // virtual drivers (BlackHole), and iPhone/iPad via Continuity.
    // ------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioObjectPropertyAddress
    {
        public uint mSelector;
        public uint mScope;
        public uint mElement;
    }

    private const uint kAudioObjectSystemObject        = 1;
    // Property selectors, scopes, and elements use four-character codes (FourCC) as
    // per the CoreAudio HAL API convention. Each uint value encodes four ASCII bytes,
    // shown in the adjacent comment, e.g. 0x64657623 == 'dev#'.
    private const uint kAudioHardwarePropertyDevices        = 0x64657623u; // 'dev#'
    private const uint kAudioHardwarePropertyDefaultInputDevice = 0x64496E20u; // 'dIn '
    private const uint kAudioDevicePropertyStreams          = 0x73746D23u; // 'stm#'
    private const uint kAudioObjectPropertyName            = 0x6C6E616Du; // 'lnam'
    private const uint kAudioDevicePropertyDeviceUID       = 0x75696420u; // 'uid '
    private const uint kAudioObjectPropertyScopeGlobal     = 0x676C6F62u; // 'glob'
    private const uint kAudioDevicePropertyScopeInput      = 0x696E7074u; // 'inpt'
    private const uint kAudioDevicePropertyScopeOutput     = 0x6F757470u; // 'outp'
    private const uint kAudioObjectPropertyElementMain     = 0;

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioObjectGetPropertyDataSize(
        uint objectId,
        in AudioObjectPropertyAddress address,
        uint qualifierDataSize,
        IntPtr qualifierData,
        out uint dataSize);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioObjectGetPropertyData(
        uint objectId,
        in AudioObjectPropertyAddress address,
        uint qualifierDataSize,
        IntPtr qualifierData,
        ref uint ioDataSize,
        IntPtr outData);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioObjectSetPropertyData(
        uint objectId,
        in AudioObjectPropertyAddress address,
        uint qualifierDataSize,
        IntPtr qualifierData,
        uint dataSize,
        IntPtr inData);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cfTypeRef);

    /// <summary>
    /// Enumerates all CoreAudio devices that have at least one stream in the requested
    /// direction (input or output).  Uses the device's persistent UID as the
    /// <see cref="AudioDevice.Id"/> so selections survive device reconnections.
    /// </summary>
    private static IReadOnlyList<AudioDevice> GetCoreAudioDevices(bool inputScope)
    {
        var devicesAddr = new AudioObjectPropertyAddress
        {
            mSelector = kAudioHardwarePropertyDevices,
            mScope    = kAudioObjectPropertyScopeGlobal,
            mElement  = kAudioObjectPropertyElementMain,
        };

        if (AudioObjectGetPropertyDataSize(kAudioObjectSystemObject, in devicesAddr, 0, IntPtr.Zero, out uint dataSize) != 0)
            return Array.Empty<AudioDevice>();

        int deviceCount = (int)(dataSize / sizeof(uint));
        if (deviceCount == 0)
            return Array.Empty<AudioDevice>();

        var deviceIds = new uint[deviceCount];
        var gch = GCHandle.Alloc(deviceIds, GCHandleType.Pinned);
        try
        {
            if (AudioObjectGetPropertyData(kAudioObjectSystemObject, in devicesAddr, 0, IntPtr.Zero, ref dataSize, gch.AddrOfPinnedObject()) != 0)
                return Array.Empty<AudioDevice>();
        }
        finally
        {
            gch.Free();
        }

        uint streamsScope = inputScope ? kAudioDevicePropertyScopeInput : kAudioDevicePropertyScopeOutput;
        var result = new List<AudioDevice>(deviceCount);

        foreach (uint deviceId in deviceIds)
        {
            // Skip devices that have no streams in the requested direction.
            var streamsAddr = new AudioObjectPropertyAddress
            {
                mSelector = kAudioDevicePropertyStreams,
                mScope    = streamsScope,
                mElement  = kAudioObjectPropertyElementMain,
            };
            if (AudioObjectGetPropertyDataSize(deviceId, in streamsAddr, 0, IntPtr.Zero, out uint streamsSize) != 0 || streamsSize == 0)
                continue;

            string? uid  = GetCoreAudioStringProperty(deviceId, kAudioDevicePropertyDeviceUID, kAudioObjectPropertyScopeGlobal);
            string? name = GetCoreAudioStringProperty(deviceId, kAudioObjectPropertyName,       kAudioObjectPropertyScopeGlobal);
            if (uid is not null && name is not null)
                result.Add(new AudioDevice(uid, name));
        }

        return result;
    }

    /// <summary>
    /// Sets the CoreAudio system-default input device to the device identified by
    /// <paramref name="uid"/>.  This makes <c>AVAudioRecorder</c> (used internally
    /// by Plugin.Maui.Audio) capture from the chosen device on the next recording start.
    /// </summary>
    private static void SetCoreAudioDefaultInputDevice(string uid)
    {
        var devicesAddr = new AudioObjectPropertyAddress
        {
            mSelector = kAudioHardwarePropertyDevices,
            mScope    = kAudioObjectPropertyScopeGlobal,
            mElement  = kAudioObjectPropertyElementMain,
        };

        if (AudioObjectGetPropertyDataSize(kAudioObjectSystemObject, in devicesAddr, 0, IntPtr.Zero, out uint dataSize) != 0)
            return;

        int deviceCount = (int)(dataSize / sizeof(uint));
        if (deviceCount == 0)
            return;

        var deviceIds = new uint[deviceCount];
        var gch = GCHandle.Alloc(deviceIds, GCHandleType.Pinned);
        try
        {
            if (AudioObjectGetPropertyData(kAudioObjectSystemObject, in devicesAddr, 0, IntPtr.Zero, ref dataSize, gch.AddrOfPinnedObject()) != 0)
                return;
        }
        finally
        {
            gch.Free();
        }

        foreach (uint deviceId in deviceIds)
        {
            var deviceUid = GetCoreAudioStringProperty(deviceId, kAudioDevicePropertyDeviceUID, kAudioObjectPropertyScopeGlobal);
            if (deviceUid != uid)
                continue;

            // Found the device — set it as the system-default input device.
            var defaultInputAddr = new AudioObjectPropertyAddress
            {
                mSelector = kAudioHardwarePropertyDefaultInputDevice,
                mScope    = kAudioObjectPropertyScopeGlobal,
                mElement  = kAudioObjectPropertyElementMain,
            };

            var idBuf = new uint[] { deviceId };
            var gch2 = GCHandle.Alloc(idBuf, GCHandleType.Pinned);
            try
            {
                AudioObjectSetPropertyData(
                    kAudioObjectSystemObject, in defaultInputAddr,
                    0, IntPtr.Zero,
                    sizeof(uint), gch2.AddrOfPinnedObject());
            }
            finally
            {
                gch2.Free();
            }
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[AudioService] SetCoreAudioDefaultInputDevice: device UID '{uid}' not found.");
    }

    /// <summary>
    /// Reads a CFString property from a CoreAudio object, converts it to a managed
    /// <see cref="string"/>, and releases the underlying CF object.
    /// </summary>
    private static string? GetCoreAudioStringProperty(uint objectId, uint selector, uint scope)
    {
        var addr = new AudioObjectPropertyAddress
        {
            mSelector = selector,
            mScope    = scope,
            mElement  = kAudioObjectPropertyElementMain,
        };

        // CFString properties are returned as a CFStringRef (a pointer-sized value).
        uint size   = (uint)IntPtr.Size;
        IntPtr buf  = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(buf, IntPtr.Zero);
            if (AudioObjectGetPropertyData(objectId, in addr, 0, IntPtr.Zero, ref size, buf) != 0)
                return null;

            IntPtr cfStringRef = Marshal.ReadIntPtr(buf);
            if (cfStringRef == IntPtr.Zero)
                return null;

            string? value = CFString.FromHandle(cfStringRef);
            CFRelease(cfStringRef);
            return value;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }
#endif
}
