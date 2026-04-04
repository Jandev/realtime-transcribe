namespace RealtimeTranscribe.Models;

/// <summary>
/// Represents a saved transcription summary file on disk.
/// </summary>
/// <param name="DisplayName">Human-readable name shown in the sidebar (date/time formatted).</param>
/// <param name="FilePath">Absolute path to the Markdown file.</param>
/// <param name="LastModified">When the file was last written.</param>
public sealed record TranscriptionFile(string DisplayName, string FilePath, DateTime LastModified);
