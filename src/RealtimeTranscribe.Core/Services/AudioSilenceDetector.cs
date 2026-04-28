using System.Buffers.Binary;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Inspects the PCM body of a 16-bit little-endian WAV payload and decides whether
/// it contains any meaningful audio.
/// </summary>
/// <remarks>
/// Used as a pre-flight gate in front of Whisper.  Whisper hallucinates fixed phrases
/// (e.g. "Thank you for watching.", "ご視聴ありがとうございました", repeated Japanese
/// cooking verbs) when fed silence or near-silence, which happens routinely with the
/// system-audio tap when nothing is playing.  Dropping silent chunks here both removes
/// those bogus transcript lines and avoids paying for the request.
///
/// Heuristic: a chunk is "silent" when EITHER its peak amplitude OR its RMS falls
/// below conservative thresholds (≈ -55 dBFS peak / -65 dBFS RMS).  Either condition
/// alone is sufficient because:
/// <list type="bullet">
///   <item>Genuinely silent / zeroed audio fails both.</item>
///   <item>Low-level dither (peak &lt; threshold) fails the peak test.</item>
///   <item>A long stretch of near-silence with sparse near-zero samples fails RMS.</item>
/// </list>
/// Thresholds are intentionally conservative so a quiet voice is still considered
/// speech, but a literal silent recording is rejected.
/// </remarks>
public static class AudioSilenceDetector
{
    // Standard PCM WAV header size; samples start at byte 44 of a canonical RIFF file.
    private const int WavHeaderBytes = 44;

    // ~ -55 dBFS in 16-bit (32767 * 10^(-55/20) ≈ 58).
    internal const short PeakAmplitudeThreshold = 60;

    // ~ -65 dBFS in 16-bit (32767 * 10^(-65/20) ≈ 18).
    internal const double RmsThreshold = 20.0;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="wavBytes"/> contains no audible
    /// signal — either because it is too short to hold any samples, or because both its
    /// peak amplitude and RMS are below the silence thresholds.
    /// </summary>
    /// <param name="wavBytes">Full WAV payload (header + 16-bit little-endian PCM data).</param>
    public static bool IsSilent(byte[] wavBytes)
    {
        if (wavBytes is null || wavBytes.Length <= WavHeaderBytes)
            return true;

        // Walk the 16-bit PCM samples after the standard RIFF header.  We deliberately do
        // not parse the header — the recorder always emits canonical 44-byte headers and
        // mis-parsing should fail closed (i.e. flag as silent) rather than send junk to
        // Whisper.
        ReadOnlySpan<byte> samples = wavBytes.AsSpan(WavHeaderBytes);
        int sampleCount = samples.Length / sizeof(short);
        if (sampleCount == 0)
            return true;

        long sumOfSquares = 0;
        int peak = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            short s = BinaryPrimitives.ReadInt16LittleEndian(samples.Slice(i * sizeof(short), sizeof(short)));
            int abs = s == short.MinValue ? short.MaxValue : Math.Abs((int)s);
            if (abs > peak)
                peak = abs;
            sumOfSquares += (long)s * s;
        }

        if (peak < PeakAmplitudeThreshold)
            return true;

        double rms = Math.Sqrt((double)sumOfSquares / sampleCount);
        return rms < RmsThreshold;
    }
}
