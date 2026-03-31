namespace RealtimeTranscribe.Models;

/// <summary>
/// Represents a single speaker turn in a diarized transcript.
/// </summary>
/// <param name="SpeakerId">Label identifying the speaker (e.g. "Speaker 1").</param>
/// <param name="Text">The spoken text attributed to this speaker.</param>
public record SpeakerSegment(string SpeakerId, string Text);
