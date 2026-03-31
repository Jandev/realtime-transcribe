using System.Text;
using System.Text.RegularExpressions;
using RealtimeTranscribe.Models;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Provides deterministic parsing and formatting of speaker-diarized transcripts.
/// </summary>
/// <remarks>
/// Expected diarized format: lines beginning with <c>Speaker N:</c> where N is a positive
/// integer (e.g. <c>Speaker 1: Hello, how is everyone?</c>).
/// </remarks>
public static class TranscriptFormatter
{
    private static readonly Regex SpeakerLinePattern =
        new(@"^(Speaker\s+\d+)\s*:\s*(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a diarized transcript string into an ordered list of <see cref="SpeakerSegment"/>
    /// objects.
    /// </summary>
    /// <remarks>
    /// Lines that do not match the <c>Speaker N:</c> pattern are appended to the text of the
    /// previous segment (continuation lines). When the input contains no speaker markers at all,
    /// a single fallback segment labelled <c>Speaker 1</c> is returned so callers always receive
    /// a non-empty list for non-empty input.
    /// </remarks>
    /// <param name="diarizedText">Raw text produced by the diarization step.</param>
    /// <returns>
    /// A read-only list of <see cref="SpeakerSegment"/> values, or an empty list when
    /// <paramref name="diarizedText"/> is <see langword="null"/>, empty, or whitespace.
    /// </returns>
    public static IReadOnlyList<SpeakerSegment> Parse(string? diarizedText)
    {
        if (string.IsNullOrWhiteSpace(diarizedText))
            return [];

        var segments = new List<SpeakerSegment>();
        string? currentSpeaker = null;
        var currentText = new StringBuilder();

        foreach (var rawLine in diarizedText.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var match = SpeakerLinePattern.Match(line);

            if (match.Success)
            {
                if (currentSpeaker is not null && currentText.Length > 0)
                    segments.Add(new SpeakerSegment(currentSpeaker, currentText.ToString().Trim()));

                currentSpeaker = match.Groups[1].Value;
                currentText.Clear();
                currentText.Append(match.Groups[2].Value);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                if (currentText.Length > 0)
                    currentText.Append(' ');
                currentText.Append(line.Trim());
            }
        }

        if (currentSpeaker is not null && currentText.Length > 0)
            segments.Add(new SpeakerSegment(currentSpeaker, currentText.ToString().Trim()));

        // Fallback: the GPT response contained no speaker markers — treat the entire input as
        // a single-speaker transcript so the caller always gets a usable result.
        if (segments.Count == 0)
            segments.Add(new SpeakerSegment("Speaker 1", diarizedText.Trim()));

        return segments;
    }

    /// <summary>
    /// Formats a list of <see cref="SpeakerSegment"/> objects into a human-readable transcript
    /// string where each turn is rendered as <c>Speaker N: text</c> on its own line.
    /// </summary>
    /// <param name="segments">Segments to format.</param>
    /// <returns>
    /// A newline-separated string, or <see cref="string.Empty"/> when
    /// <paramref name="segments"/> is empty.
    /// </returns>
    public static string Format(IReadOnlyList<SpeakerSegment> segments)
    {
        if (segments.Count == 0)
            return string.Empty;

        return string.Join(Environment.NewLine, segments.Select(s => $"{s.SpeakerId}: {s.Text}"));
    }
}
