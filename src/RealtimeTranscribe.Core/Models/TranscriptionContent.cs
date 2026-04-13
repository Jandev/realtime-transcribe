namespace RealtimeTranscribe.Models;

/// <summary>
/// Represents the parsed content of a transcription markdown file,
/// split into its constituent sections.
/// </summary>
/// <param name="Summary">The summary and action items section content.</param>
/// <param name="Transcript">The raw transcript section content.</param>
/// <param name="DiarizedTranscript">The speaker-attributed transcript section content.</param>
public sealed record TranscriptionContent(string Summary, string Transcript, string DiarizedTranscript);
