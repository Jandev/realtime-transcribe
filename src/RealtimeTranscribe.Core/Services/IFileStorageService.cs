using RealtimeTranscribe.Models;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Persists summary and action-item files to a user-configured folder on disk.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Gets or sets the folder where summary files are written.</summary>
    string? OutputFolder { get; set; }

    /// <summary>
    /// Writes all transcription data to a Markdown file named
    /// <c>yyyyMMdd HHmm.md</c> in <see cref="OutputFolder"/>.
    /// The file contains three sections: Summary and action items, Transcript,
    /// and Speaker attributed transcript.
    /// Does nothing when <see cref="OutputFolder"/> is null or empty,
    /// or when all content parameters are null or empty.
    /// </summary>
    Task SaveTranscriptionAsync(string? summary, string? transcript, string? diarizedTranscript, DateTime timestamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all Markdown files in <see cref="OutputFolder"/>, ordered newest-first.
    /// Returns an empty list when <see cref="OutputFolder"/> is null, empty, or does not exist.
    /// </summary>
    Task<IReadOnlyList<TranscriptionFile>> ListSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and returns the text content of the file at <paramref name="filePath"/>.
    /// </summary>
    Task<string> LoadSummaryAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the file at <paramref name="filePath"/> and parses it into separate sections
    /// (Summary, Transcript, DiarizedTranscript).
    /// </summary>
    Task<TranscriptionContent> LoadTranscriptionAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the file at <paramref name="oldFilePath"/> to <paramref name="newName"/>.md
    /// in the same directory.  Returns the updated <see cref="TranscriptionFile"/>.
    /// </summary>
    Task<TranscriptionFile> RenameSummaryAsync(string oldFilePath, string newName, CancellationToken cancellationToken = default);
}
