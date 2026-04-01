namespace RealtimeTranscribe.Services;

/// <summary>
/// Persists summary and action-item files to a user-configured folder on disk.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Gets or sets the folder where summary files are written.</summary>
    string? OutputFolder { get; set; }

    /// <summary>
    /// Writes <paramref name="summary"/> to a Markdown file named
    /// <c>yyyyMMdd HHmm.md</c> in <see cref="OutputFolder"/>.
    /// Does nothing when <see cref="OutputFolder"/> is null or empty,
    /// or when <paramref name="summary"/> is null or empty.
    /// </summary>
    Task SaveSummaryAsync(string summary, DateTime timestamp, CancellationToken cancellationToken = default);
}
