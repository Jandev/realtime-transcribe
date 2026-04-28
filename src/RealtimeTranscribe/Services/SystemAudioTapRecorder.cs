#if MACCATALYST
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CoreFoundation;
using Foundation;
using ObjCRuntime;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Captures the system audio mix (everything the user hears, from any application) on
/// macOS 14.2+ using Apple's CoreAudio Process Tap API.  Unlike the BlackHole / Multi-Output
/// Device workaround, this works with any output device the user has selected — including
/// AirPods and other Bluetooth headphones — because the tap intercepts audio frames inside
/// CoreAudio before they are handed to the output driver.
/// <para>
/// Pipeline: <c>CATapDescription</c> → <c>AudioHardwareCreateProcessTap</c> →
/// private aggregate device whose <c>kAudioAggregateDeviceTapListKey</c> contains the tap →
/// <c>AudioDeviceCreateIOProcID</c> + <c>AudioDeviceStart</c> → IOProc copies Float32 PCM
/// frames into an in-memory buffer → <see cref="StopAsync"/> / <see cref="GetChunk"/> wrap
/// the captured bytes in a 16-bit PCM WAV container compatible with the rest of the
/// transcription pipeline.
/// </para>
/// <para>
/// Requires the <c>NSAudioCaptureUsageDescription</c> Info.plist key — the OS prompts the
/// user the first time the tap is created.  Microphone permission
/// (<c>NSMicrophoneUsageDescription</c> / TCC) is independent of this.
/// </para>
/// </summary>
internal sealed class SystemAudioTapRecorder : IDisposable
{
    // Synchronises access to the PCM buffer from the real-time IOProc thread and the
    // managed Start/Stop/GetChunk callers.  All buffer mutation MUST happen under this lock.
    private readonly object _lock = new();

    // Captured audio is accumulated as raw little-endian 16-bit PCM samples here.
    // The WAV header is prepended on retrieval so we can stream a single chunk without
    // needing to know the length up front.
    private MemoryStream _pcmBuffer = new();

    // CoreAudio handles owned by this recorder.  Disposed in the strict reverse order of
    // creation (Stop ▷ Destroy IOProc ▷ Destroy Aggregate ▷ Destroy Tap ▷ Release CATapDescription).
    private uint _tapId;
    private uint _aggregateDeviceId;
    private IntPtr _ioProcId;
    private IntPtr _tapDescriptionHandle;

    // Pinned delegate so the GC doesn't move/collect it while the audio thread is using it.
    private AudioDeviceIOProcDelegate? _ioProcDelegate;
    private GCHandle _ioProcGcHandle;

    // Stream format of the aggregate device's input scope, queried after creation.
    // Used to (a) compute the WAV header sample-rate / channel count, and (b) decide how
    // to walk the per-callback AudioBufferList (interleaved vs. planar).
    private double _sampleRate;
    private uint _channelsPerFrame;
    private uint _bitsPerChannel;
    private bool _isFloatFormat;
    private bool _isInterleaved;

    public bool IsRecording { get; private set; }

    public Task StartAsync()
    {
        if (IsRecording)
            return Task.CompletedTask;

        // ---- 1. Create the CATapDescription and the tap itself ---------------------
        // initStereoGlobalTapButExcludeProcesses: with our own pid in the exclude list
        // ⇒ capture every other process's audio mixed down to stereo.  We exclude self
        // so that any sounds the app itself plays (e.g. UI feedback) cannot loop back
        // into the recording.
        _tapDescriptionHandle = CreateTapDescriptionExcludingSelf();
        if (_tapDescriptionHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                "Failed to create CATapDescription. macOS 14.2+ is required for system-audio capture.");

        int status = AudioHardwareCreateProcessTap(_tapDescriptionHandle, out _tapId);
        if (status != 0 || _tapId == 0)
        {
            ReleaseObject(ref _tapDescriptionHandle);
            throw new InvalidOperationException(
                $"AudioHardwareCreateProcessTap failed (OSStatus {status}{FormatOsStatusFourCc(status)}). " +
                "This usually means one of the following:\n" +
                "  • The app is missing system-audio capture permission. Open System Settings → " +
                "Privacy & Security → Screen & System Audio Recording (or Screen Recording on macOS 14), " +
                "find Realtime Transcribe, toggle it on, then quit and relaunch the app.\n" +
                "  • The app is running inside the macOS App Sandbox. The Process Tap API does not " +
                "work in sandboxed apps, even with the TCC permission granted. Use the released, " +
                "non-sandboxed .app bundle from GitHub Releases (or build it yourself — see " +
                "docs/system-audio-setup.md).\n" +
                "  • You are running macOS older than 14.2. Use the BlackHole-based fallback instead " +
                "(see docs/blackhole-setup.md).");
        }

        // ---- 2. Build a private aggregate device that contains the tap ------------
        var tapUid = GetStringProperty(_tapId, kAudioTapPropertyUID, kAudioObjectPropertyScopeGlobal);
        if (tapUid is null)
        {
            DestroyAll();
            throw new InvalidOperationException("Failed to read kAudioTapPropertyUID from new tap.");
        }

        using var aggDict = new NSMutableDictionary
        {
            [(NSString)kAudioAggregateDeviceUIDKey]       = (NSString)("com.jandev.realtimetranscribe.tap." + Guid.NewGuid().ToString("N")),
            [(NSString)kAudioAggregateDeviceNameKey]      = (NSString)"RealtimeTranscribe System Audio",
            [(NSString)kAudioAggregateDeviceIsPrivateKey] = NSNumber.FromBoolean(true),
            [(NSString)kAudioAggregateDeviceIsStackedKey] = NSNumber.FromBoolean(false),
        };

        using var subTapDict = new NSMutableDictionary
        {
            [(NSString)kAudioSubTapUIDKey]                  = (NSString)tapUid,
            [(NSString)kAudioSubTapDriftCompensationKey]    = NSNumber.FromBoolean(false),
        };
        using var tapList = NSArray.FromNSObjects(subTapDict)!;
        aggDict[(NSString)kAudioAggregateDeviceTapListKey] = tapList;

        status = AudioHardwareCreateAggregateDevice(aggDict.Handle, out _aggregateDeviceId);
        if (status != 0 || _aggregateDeviceId == 0)
        {
            DestroyAll();
            throw new InvalidOperationException(
                $"AudioHardwareCreateAggregateDevice failed (OSStatus {status}).");
        }

        // ---- 3. Query the input stream format so we can transcode to 16-bit WAV ---
        if (!QueryStreamFormat(_aggregateDeviceId,
                out _sampleRate, out _channelsPerFrame, out _bitsPerChannel,
                out _isFloatFormat, out _isInterleaved))
        {
            DestroyAll();
            throw new InvalidOperationException("Failed to query aggregate device input stream format.");
        }

        // ---- 4. Install the IOProc and start the device ----------------------------
        _ioProcDelegate = AudioIOProc;
        // Pin the delegate to keep it alive while CoreAudio's real-time thread invokes it.
        _ioProcGcHandle = GCHandle.Alloc(_ioProcDelegate);

        status = AudioDeviceCreateIOProcID(_aggregateDeviceId, _ioProcDelegate, IntPtr.Zero, out _ioProcId);
        if (status != 0 || _ioProcId == IntPtr.Zero)
        {
            DestroyAll();
            throw new InvalidOperationException($"AudioDeviceCreateIOProcID failed (OSStatus {status}).");
        }

        status = AudioDeviceStart(_aggregateDeviceId, _ioProcId);
        if (status != 0)
        {
            DestroyAll();
            throw new InvalidOperationException($"AudioDeviceStart failed (OSStatus {status}).");
        }

        IsRecording = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Snapshots the audio captured since the last call (or since <see cref="StartAsync"/>),
    /// returns it as a complete 16-bit PCM WAV byte array, and resets the internal buffer
    /// so the next call returns only newly-captured audio.  Capture continues uninterrupted
    /// — unlike the AVAudioRecorder path we do not stop/restart the underlying recorder.
    /// </summary>
    public byte[] GetChunk()
    {
        if (!IsRecording)
            return Array.Empty<byte>();

        byte[] pcm;
        lock (_lock)
        {
            pcm = _pcmBuffer.ToArray();
            // Replace the buffer rather than reset its position so the consumer's snapshot
            // is independent of any further IOProc writes that arrive after we exit the lock.
            _pcmBuffer = new MemoryStream();
        }

        return pcm.Length == 0 ? Array.Empty<byte>() : WrapInWavContainer(pcm);
    }

    public Task<byte[]> StopAsync()
    {
        if (!IsRecording)
            return Task.FromResult(Array.Empty<byte>());

        // AudioDeviceStop is synchronous: when it returns, no further IOProc callbacks
        // are in flight, so the buffer can be drained safely without a lock.
        AudioDeviceStop(_aggregateDeviceId, _ioProcId);
        IsRecording = false;

        byte[] pcm;
        lock (_lock)
        {
            pcm = _pcmBuffer.ToArray();
            _pcmBuffer = new MemoryStream();
        }

        DestroyAll();

        return Task.FromResult(pcm.Length == 0 ? Array.Empty<byte>() : WrapInWavContainer(pcm));
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            try { AudioDeviceStop(_aggregateDeviceId, _ioProcId); }
            catch { /* best effort */ }
            IsRecording = false;
        }
        DestroyAll();
        GC.SuppressFinalize(this);
    }

    // ---------------------------------------------------------------------
    // IOProc — invoked on a real-time CoreAudio thread for each input cycle.
    //
    // Performance notes:
    //   • This runs on a high-priority real-time thread.  Allocations and locks must be
    //     minimal — we lock only briefly while appending into the MemoryStream.
    //   • We always emit interleaved 16-bit PCM into _pcmBuffer regardless of the source
    //     layout so the WAV writer doesn't need to know about the input format.
    // ---------------------------------------------------------------------
    private int AudioIOProc(
        uint inDevice,
        IntPtr inNow,
        IntPtr inInputData,
        IntPtr inInputTime,
        IntPtr outOutputData,
        IntPtr inOutputTime,
        IntPtr inClientData)
    {
        if (inInputData == IntPtr.Zero)
            return 0;

        try
        {
            uint numberBuffers = (uint)Marshal.ReadInt32(inInputData);
            if (numberBuffers == 0)
                return 0;

            // AudioBufferList layout: { UInt32 mNumberBuffers; AudioBuffer mBuffers[1]; }
            // AudioBuffer  layout:    { UInt32 mNumberChannels; UInt32 mDataByteSize; void* mData; }
            // mBuffers starts at offset 4; each AudioBuffer is 8 + IntPtr.Size bytes.
            int audioBufferSize = 8 + IntPtr.Size;
            int channels = (int)_channelsPerFrame;
            if (channels == 0)
                channels = 1;

            // Determine the frame count from the first buffer.
            // Interleaved: one buffer with mNumberChannels == channels, mDataByteSize == frames * channels * 4.
            // Planar:      `channels` buffers each with mNumberChannels == 1, mDataByteSize == frames * 4.
            IntPtr firstBufferPtr = IntPtr.Add(inInputData, 4);
            uint firstChannelsInBuffer = (uint)Marshal.ReadInt32(firstBufferPtr, 0);
            uint firstDataBytes        = (uint)Marshal.ReadInt32(firstBufferPtr, 4);
            int  bytesPerSample        = (int)(_bitsPerChannel / 8);
            if (bytesPerSample == 0)
                bytesPerSample = 4; // sane fallback for Float32

            int firstChannelStride = (int)Math.Max(1u, firstChannelsInBuffer);
            int frameCount = (int)(firstDataBytes / (uint)(firstChannelStride * bytesPerSample));
            if (frameCount <= 0)
                return 0;

            // Build interleaved Int16 output.
            int outBytes = frameCount * channels * sizeof(short);
            byte[] outBuf = new byte[outBytes];
            Span<byte> outSpan = outBuf;

            if (_isInterleaved || numberBuffers == 1)
            {
                // Single buffer carries `channels` interleaved samples per frame.
                IntPtr data = Marshal.ReadIntPtr(firstBufferPtr, 8);
                if (data == IntPtr.Zero)
                    return 0;

                int sourceChannels = (int)Math.Max(1u, firstChannelsInBuffer);
                ConvertInterleavedToInt16(data, frameCount, sourceChannels, channels, outSpan);
            }
            else
            {
                // Planar: one buffer per channel.  Walk all `numberBuffers` buffers,
                // capping at the requested channel count.
                int planes = (int)Math.Min(numberBuffers, (uint)channels);
                IntPtr[] planePtrs = new IntPtr[planes];
                for (int p = 0; p < planes; p++)
                {
                    IntPtr buf = IntPtr.Add(inInputData, 4 + p * audioBufferSize);
                    planePtrs[p] = Marshal.ReadIntPtr(buf, 8);
                    if (planePtrs[p] == IntPtr.Zero)
                        return 0;
                }
                ConvertPlanarToInt16(planePtrs, frameCount, channels, outSpan);
            }

            lock (_lock)
            {
                _pcmBuffer.Write(outBuf, 0, outBytes);
            }
        }
        catch (Exception ex)
        {
            // Never let an exception escape into the CoreAudio thread.
            Debug.WriteLine($"[SystemAudioTapRecorder] IOProc error: {ex}");
        }

        return 0;
    }

    private void ConvertInterleavedToInt16(IntPtr src, int frameCount, int sourceChannels, int outChannels, Span<byte> dest)
    {
        // Copy/duplicate as needed so the output always has `outChannels` channels.
        int outChannelsToCopy = Math.Min(sourceChannels, outChannels);
        for (int frame = 0; frame < frameCount; frame++)
        {
            int destFrameOffset = frame * outChannels * sizeof(short);
            for (int ch = 0; ch < outChannelsToCopy; ch++)
            {
                short s = ReadSampleAsInt16(src, frame * sourceChannels + ch);
                BinaryPrimitives.WriteInt16LittleEndian(dest.Slice(destFrameOffset + ch * sizeof(short), 2), s);
            }
            // Pad any remaining output channels with the last copied sample (mono → stereo, etc.).
            for (int ch = outChannelsToCopy; ch < outChannels; ch++)
            {
                short s = outChannelsToCopy > 0 ? ReadSampleAsInt16(src, frame * sourceChannels + outChannelsToCopy - 1) : (short)0;
                BinaryPrimitives.WriteInt16LittleEndian(dest.Slice(destFrameOffset + ch * sizeof(short), 2), s);
            }
        }
    }

    private void ConvertPlanarToInt16(IntPtr[] planes, int frameCount, int outChannels, Span<byte> dest)
    {
        int planeCount = planes.Length;
        for (int frame = 0; frame < frameCount; frame++)
        {
            int destFrameOffset = frame * outChannels * sizeof(short);
            for (int ch = 0; ch < outChannels; ch++)
            {
                int sourcePlane = ch < planeCount ? ch : planeCount - 1;
                short s = ReadSampleAsInt16(planes[sourcePlane], frame);
                BinaryPrimitives.WriteInt16LittleEndian(dest.Slice(destFrameOffset + ch * sizeof(short), 2), s);
            }
        }
    }

    private short ReadSampleAsInt16(IntPtr basePtr, int sampleIndex)
    {
        if (_isFloatFormat && _bitsPerChannel == 32)
        {
            // Float32 sample, range typically [-1, 1].  Saturate to int16.
            float f = Marshal.PtrToStructure<float>(IntPtr.Add(basePtr, sampleIndex * 4));
            int v = (int)Math.Round(Math.Clamp(f, -1f, 1f) * short.MaxValue);
            return (short)v;
        }
        if (!_isFloatFormat && _bitsPerChannel == 16)
        {
            return (short)Marshal.ReadInt16(IntPtr.Add(basePtr, sampleIndex * 2));
        }
        if (!_isFloatFormat && _bitsPerChannel == 32)
        {
            // Int32 PCM — narrow to 16-bit by right-shift.
            int v = Marshal.ReadInt32(IntPtr.Add(basePtr, sampleIndex * 4));
            return (short)(v >> 16);
        }
        // Unsupported format — emit silence rather than corrupt audio.
        return 0;
    }

    /// <summary>
    /// Wraps the accumulated raw little-endian 16-bit PCM bytes in a minimal
    /// RIFF/WAV container compatible with the rest of the transcription pipeline.
    /// </summary>
    private byte[] WrapInWavContainer(byte[] pcm)
    {
        int channels = (int)Math.Max(1u, _channelsPerFrame);
        int sampleRate = (int)Math.Round(_sampleRate > 0 ? _sampleRate : 48000);
        const int bitsPerSample = 16;
        int byteRate    = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);
        int dataSize    = pcm.Length;
        int riffSize    = 36 + dataSize;

        using var ms = new MemoryStream(44 + dataSize);
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write("RIFF"u8.ToArray());
            w.Write(riffSize);
            w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray());
            w.Write(16);                       // PCM fmt chunk size
            w.Write((short)1);                 // PCM format
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write(blockAlign);
            w.Write((short)bitsPerSample);
            w.Write("data"u8.ToArray());
            w.Write(dataSize);
            w.Write(pcm);
        }
        return ms.ToArray();
    }

    // ---------------------------------------------------------------------
    // CoreAudio teardown — safe to call from any state, including partial init.
    // ---------------------------------------------------------------------
    private void DestroyAll()
    {
        if (_ioProcId != IntPtr.Zero && _aggregateDeviceId != 0)
        {
            try { AudioDeviceDestroyIOProcID(_aggregateDeviceId, _ioProcId); }
            catch { /* best effort */ }
            _ioProcId = IntPtr.Zero;
        }

        if (_aggregateDeviceId != 0)
        {
            try { AudioHardwareDestroyAggregateDevice(_aggregateDeviceId); }
            catch { /* best effort */ }
            _aggregateDeviceId = 0;
        }

        if (_tapId != 0)
        {
            try { AudioHardwareDestroyProcessTap(_tapId); }
            catch { /* best effort */ }
            _tapId = 0;
        }

        ReleaseObject(ref _tapDescriptionHandle);

        if (_ioProcGcHandle.IsAllocated)
            _ioProcGcHandle.Free();
        _ioProcDelegate = null;
    }

    // ---------------------------------------------------------------------
    // Objective-C runtime helpers — we use these instead of pulling in a
    // Xamarin binding for CATapDescription, which is not currently shipped
    // in the .NET 10 MAUI Mac Catalyst SDK.
    // ---------------------------------------------------------------------
    private static IntPtr CreateTapDescriptionExcludingSelf()
    {
        IntPtr cls = Class.GetHandle("CATapDescription");
        if (cls == IntPtr.Zero)
            return IntPtr.Zero; // CATapDescription not available (pre-macOS 14.2)

        // NSArray of one NSNumber wrapping our pid.
        int pid = Process.GetCurrentProcess().Id;
        using var pidNum = NSNumber.FromInt32(pid);
        using var excludeArr = NSArray.FromNSObjects(pidNum)!;

        IntPtr alloc = IntPtr_objc_msgSend(cls, Selector.GetHandle("alloc"));
        if (alloc == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr desc = IntPtr_objc_msgSend_IntPtr(
            alloc,
            Selector.GetHandle("initStereoGlobalTapButExcludeProcesses:"),
            excludeArr.Handle);

        return desc;
    }

    private static void ReleaseObject(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;
        IntPtr_objc_msgSend(handle, Selector.GetHandle("release"));
        handle = IntPtr.Zero;
    }

    /// <summary>
    /// Decodes a CoreAudio <c>OSStatus</c> as its FourCC representation when the four
    /// bytes are printable ASCII (e.g. <c>'what'</c> for <c>kAudioHardwareIllegalOperationError</c>).
    /// Returns an empty string for non-printable codes so the caller can append unconditionally.
    /// </summary>
    private static string FormatOsStatusFourCc(int status)
    {
        if (status == 0)
            return string.Empty;

        Span<byte> bytes = stackalloc byte[4];
        bytes[0] = (byte)((status >> 24) & 0xFF);
        bytes[1] = (byte)((status >> 16) & 0xFF);
        bytes[2] = (byte)((status >> 8)  & 0xFF);
        bytes[3] = (byte)(status         & 0xFF);

        for (int i = 0; i < 4; i++)
        {
            if (bytes[i] < 0x20 || bytes[i] > 0x7E)
                return string.Empty;
        }

        return $" / '{(char)bytes[0]}{(char)bytes[1]}{(char)bytes[2]}{(char)bytes[3]}'";
    }

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    // ---------------------------------------------------------------------
    // CoreAudio constants & P/Invoke (Process Tap + Aggregate Device + IOProc)
    //
    // String keys for kAudioAggregateDevice* / kAudioSubTap* are documented as the
    // exact ASCII strings used here ("uid", "name", "private", "stacked", "taps", "drift").
    // FourCC selectors are encoded big-endian to match CoreAudio's convention.
    // ---------------------------------------------------------------------
    private const string kAudioAggregateDeviceUIDKey       = "uid";
    private const string kAudioAggregateDeviceNameKey      = "name";
    private const string kAudioAggregateDeviceIsPrivateKey = "private";
    private const string kAudioAggregateDeviceIsStackedKey = "stacked";
    private const string kAudioAggregateDeviceTapListKey   = "taps";
    private const string kAudioSubTapUIDKey                = "uid";
    private const string kAudioSubTapDriftCompensationKey  = "drift";

    private const uint kAudioObjectPropertyScopeGlobal = 0x676C6F62u; // 'glob'
    private const uint kAudioObjectPropertyScopeInput  = 0x696E7074u; // 'inpt'
    private const uint kAudioObjectPropertyElementMain = 0;
    private const uint kAudioTapPropertyUID            = 0x74756964u; // 'tuid'
    private const uint kAudioObjectPropertyName        = 0x6C6E616Du; // 'lnam'
    private const uint kAudioDevicePropertyStreamFormat = 0x73666D74u; // 'sfmt'

    // AudioFormatFlags
    private const uint kAudioFormatFlagIsFloat        = 1u << 0;
    private const uint kAudioFormatFlagIsNonInterleaved = 1u << 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioObjectPropertyAddress
    {
        public uint mSelector;
        public uint mScope;
        public uint mElement;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStreamBasicDescription
    {
        public double mSampleRate;
        public uint   mFormatID;
        public uint   mFormatFlags;
        public uint   mBytesPerPacket;
        public uint   mFramesPerPacket;
        public uint   mBytesPerFrame;
        public uint   mChannelsPerFrame;
        public uint   mBitsPerChannel;
        public uint   mReserved;
    }

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioHardwareCreateProcessTap(IntPtr description, out uint outTapId);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioHardwareDestroyProcessTap(uint tapId);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioHardwareCreateAggregateDevice(IntPtr dictionary, out uint outDeviceId);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioHardwareDestroyAggregateDevice(uint deviceId);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioObjectGetPropertyDataSize(
        uint objectId, in AudioObjectPropertyAddress address,
        uint qualifierDataSize, IntPtr qualifierData, out uint dataSize);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioObjectGetPropertyData(
        uint objectId, in AudioObjectPropertyAddress address,
        uint qualifierDataSize, IntPtr qualifierData,
        ref uint ioDataSize, IntPtr outData);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioDeviceCreateIOProcID(
        uint deviceId,
        AudioDeviceIOProcDelegate proc,
        IntPtr clientData,
        out IntPtr outIOProcId);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioDeviceDestroyIOProcID(uint deviceId, IntPtr ioProcId);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioDeviceStart(uint deviceId, IntPtr ioProcId);

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioDeviceStop(uint deviceId, IntPtr ioProcId);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cfTypeRef);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AudioDeviceIOProcDelegate(
        uint inDevice,
        IntPtr inNow,
        IntPtr inInputData,
        IntPtr inInputTime,
        IntPtr outOutputData,
        IntPtr inOutputTime,
        IntPtr inClientData);

    private static bool QueryStreamFormat(uint deviceId,
        out double sampleRate, out uint channels, out uint bitsPerChannel,
        out bool isFloat, out bool isInterleaved)
    {
        sampleRate = 0;
        channels = 0;
        bitsPerChannel = 0;
        isFloat = false;
        isInterleaved = true;

        var addr = new AudioObjectPropertyAddress
        {
            mSelector = kAudioDevicePropertyStreamFormat,
            mScope    = kAudioObjectPropertyScopeInput,
            mElement  = kAudioObjectPropertyElementMain,
        };

        uint size = (uint)Marshal.SizeOf<AudioStreamBasicDescription>();
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            int status = AudioObjectGetPropertyData(deviceId, in addr, 0, IntPtr.Zero, ref size, buf);
            if (status != 0)
                return false;

            var desc = Marshal.PtrToStructure<AudioStreamBasicDescription>(buf);
            sampleRate     = desc.mSampleRate;
            channels       = desc.mChannelsPerFrame;
            bitsPerChannel = desc.mBitsPerChannel;
            isFloat        = (desc.mFormatFlags & kAudioFormatFlagIsFloat) != 0;
            isInterleaved  = (desc.mFormatFlags & kAudioFormatFlagIsNonInterleaved) == 0;
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static string? GetStringProperty(uint objectId, uint selector, uint scope)
    {
        var addr = new AudioObjectPropertyAddress
        {
            mSelector = selector,
            mScope    = scope,
            mElement  = kAudioObjectPropertyElementMain,
        };

        uint size = (uint)IntPtr.Size;
        IntPtr buf = Marshal.AllocHGlobal(IntPtr.Size);
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
}
#endif
