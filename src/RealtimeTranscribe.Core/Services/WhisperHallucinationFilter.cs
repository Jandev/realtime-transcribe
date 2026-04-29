using System.Text.RegularExpressions;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Removes well-known Whisper hallucination phrases from a transcript.
/// </summary>
/// <remarks>
/// Whisper systematically emits a small set of fixed phrases (or near-repeats of a
/// single token) when fed silence or near-silence — most famously
/// "Thank you for watching." in English and "ご視聴ありがとうございました" in Japanese,
/// plus repeated cooking verbs like "混ぜる 混ぜる 混ぜる …".  These are widely
/// documented in the Whisper / OpenAI community.  They have nothing to do with the
/// actual audio and must not be appended to the user's transcript.
///
/// This filter is the second line of defence behind <see cref="AudioSilenceDetector"/>
/// — anything that slips past the silence pre-check (because the chunk contained a
/// genuine but tiny snippet of audio) is still cleaned here.
/// </remarks>
public static class WhisperHallucinationFilter
{
    // A token is considered "trivially repeating" when the same word repeats this many
    // times or more across a single transcript.  Whisper's repetition-on-silence
    // failure mode produces dozens of identical tokens; legitimate speech almost never
    // contains a single word repeated this often in a 30-second window.
    private const int MaxAllowedTokenRepetitions = 5;

    // Curated list of canonical hallucination phrases, all matched case-insensitively
    // and ignoring trailing punctuation / whitespace.  Sourced from openai/whisper
    // issues (#928, #1762, #2173) and Hugging Face transformers#28285.
    // Multi-language coverage matches the languages this app actually transcribes
    // (Dutch / English) plus the most common foreign-language hallucinations Whisper
    // emits even on Dutch / English audio (Japanese, Chinese, Korean YouTube outros).
    private static readonly HashSet<string> KnownHallucinations = new(StringComparer.OrdinalIgnoreCase)
    {
        // English
        "thank you",
        "thank you.",
        "thanks for watching",
        "thanks for watching!",
        "thank you for watching",
        "thank you for watching.",
        "thank you for watching!",
        "thank you so much for watching",
        "please subscribe",
        "like and subscribe",
        "see you next time",
        "see you in the next video",
        "bye",
        "bye bye",
        "you",
        ".",
        // Dutch
        "bedankt voor het kijken",
        "bedankt voor het kijken.",
        "ondertiteld door",
        // Japanese (the cooking-verb / outro hallucinations the user reported)
        "ご視聴ありがとうございました",
        "ご視聴ありがとうございました。",
        "ご清聴ありがとうございました",
        "ありがとうございました",
        "次回もお楽しみに",
        // Chinese
        "请订阅",
        "感谢观看",
        "谢谢观看",
        // Korean
        "시청해주셔서 감사합니다",
    };

    /// <summary>
    /// Returns <paramref name="transcript"/> with known hallucinations stripped.  If the
    /// resulting text is empty or whitespace-only, returns <see cref="string.Empty"/> so
    /// the caller can treat it as "no transcript".
    /// </summary>
    public static string Filter(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return string.Empty;

        // 1. Whole-string match: the entire response (after trim) is a known phrase.
        string trimmed = transcript.Trim();
        if (IsKnownHallucination(trimmed))
            return string.Empty;

        // 2. Line-by-line filter: drop any individual line that is itself a known phrase
        //    (Whisper sometimes emits the hallucination on its own line at the end of
        //    otherwise-real speech).
        var keptLines = new List<string>();
        foreach (var line in transcript.Split('\n'))
        {
            string lineTrimmed = line.Trim();
            if (lineTrimmed.Length == 0)
                continue;
            if (IsKnownHallucination(lineTrimmed))
                continue;
            keptLines.Add(line);
        }

        string filtered = string.Join('\n', keptLines).Trim();
        if (filtered.Length == 0)
            return string.Empty;

        // 3. Repetition collapse: when the same non-trivial token repeats absurdly often
        //    in a single chunk it's the "混ぜる 混ぜる 混ぜる …" failure mode.  Treat the
        //    whole chunk as a hallucination rather than try to salvage part of it,
        //    because by the time Whisper enters that mode the rest of the output is
        //    unreliable too.
        if (IsTriviallyRepeating(filtered))
            return string.Empty;

        return filtered;
    }

    private static bool IsKnownHallucination(string candidate)
    {
        // Strip trailing punctuation so "Thank you for watching" matches
        // "Thank you for watching." / "Thank you for watching!" / "Thank you for watching?".
        string normalised = candidate.TrimEnd('.', '!', '?', '。', '！', '？', ' ', '\t');
        if (normalised.Length == 0)
            return true;
        return KnownHallucinations.Contains(normalised) || KnownHallucinations.Contains(candidate);
    }

    private static bool IsTriviallyRepeating(string text)
    {
        // Split on Unicode whitespace.  Tokens shorter than 2 characters are ignored
        // (single punctuation marks, "I", "a", filler particles) so we only flag
        // genuine word-level repetition.
        var tokens = Regex.Split(text, @"\s+");
        if (tokens.Length < MaxAllowedTokenRepetitions)
            return false;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            string t = token.Trim('.', '!', '?', '。', '！', '？', ',', '、');
            if (t.Length < 2)
                continue;
            counts[t] = counts.TryGetValue(t, out int c) ? c + 1 : 1;
            if (counts[t] >= MaxAllowedTokenRepetitions)
                return true;
        }
        return false;
    }
}
