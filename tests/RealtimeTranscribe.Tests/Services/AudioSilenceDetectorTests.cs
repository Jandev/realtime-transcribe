using System.Buffers.Binary;
using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AudioSilenceDetector"/>.
/// Builds canonical 16-bit mono WAV payloads in memory so the detector is exercised
/// against the same byte layout the recorder produces.
/// </summary>
public class AudioSilenceDetectorTests
{
    private const int HeaderSize = 44;
    private const int SampleRate = 48000;

    private static byte[] BuildWav(short[] samples)
    {
        // We don't test header-parsing correctness here — the detector intentionally
        // skips the first 44 bytes — so the header bytes can be left zeroed.
        byte[] wav = new byte[HeaderSize + samples.Length * sizeof(short)];
        for (int i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(HeaderSize + i * 2, 2), samples[i]);
        return wav;
    }

    [Fact]
    public void IsSilent_ReturnsTrue_ForNullInput()
    {
        Assert.True(AudioSilenceDetector.IsSilent(null!));
    }

    [Fact]
    public void IsSilent_ReturnsTrue_ForHeaderOnlyPayload()
    {
        Assert.True(AudioSilenceDetector.IsSilent(new byte[HeaderSize]));
    }

    [Fact]
    public void IsSilent_ReturnsTrue_ForAllZeroSamples()
    {
        var wav = BuildWav(new short[SampleRate]); // 1 s of silence
        Assert.True(AudioSilenceDetector.IsSilent(wav));
    }

    [Fact]
    public void IsSilent_ReturnsTrue_ForLowAmplitudeNoise()
    {
        // Quantisation-level noise — every sample alternates ±10, well below
        // the peak threshold (~60).
        var samples = new short[SampleRate];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)((i % 2 == 0) ? 10 : -10);
        var wav = BuildWav(samples);
        Assert.True(AudioSilenceDetector.IsSilent(wav));
    }

    [Fact]
    public void IsSilent_ReturnsFalse_ForSineToneAtModerateLevel()
    {
        // 440 Hz sine at ~ -20 dBFS — clearly speech-equivalent loudness.
        int sampleCount = SampleRate; // 1 second
        var samples = new short[sampleCount];
        double amp = short.MaxValue * 0.1; // -20 dBFS
        for (int i = 0; i < sampleCount; i++)
            samples[i] = (short)(amp * Math.Sin(2 * Math.PI * 440 * i / SampleRate));
        var wav = BuildWav(samples);
        Assert.False(AudioSilenceDetector.IsSilent(wav));
    }

    [Fact]
    public void IsSilent_ReturnsFalse_ForLoudFullScaleSignal()
    {
        var samples = new short[SampleRate];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)((i % 2 == 0) ? short.MaxValue / 2 : -short.MaxValue / 2);
        var wav = BuildWav(samples);
        Assert.False(AudioSilenceDetector.IsSilent(wav));
    }
}
