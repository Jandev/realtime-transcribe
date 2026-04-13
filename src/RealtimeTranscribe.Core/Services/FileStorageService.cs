using System.Text;
using RealtimeTranscribe.Models;

namespace RealtimeTranscribe.Services;

/// <inheritdoc cref="IFileStorageService"/>
public class FileStorageService : IFileStorageService
{
    /// <inheritdoc/>
    public string? OutputFolder { get; set; }

    /// <inheritdoc/>
    public async Task SaveSummaryAsync(string summary, DateTime timestamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(OutputFolder) || string.IsNullOrEmpty(summary))
            return;

        var fileName = timestamp.ToString("yyyyMMdd HHmm") + ".md";
        var filePath = Path.Combine(OutputFolder, fileName);

        await File.WriteAllTextAsync(filePath, summary, Encoding.UTF8, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TranscriptionFile>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(OutputFolder) || !Directory.Exists(OutputFolder))
            return Task.FromResult<IReadOnlyList<TranscriptionFile>>(Array.Empty<TranscriptionFile>());

        var files = new DirectoryInfo(OutputFolder)
            .GetFiles("*.md")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new TranscriptionFile(
                DisplayName: FormatDisplayName(f.Name, f.LastWriteTime),
                FilePath: f.FullName,
                LastModified: f.LastWriteTime))
            .ToList();

        return Task.FromResult<IReadOnlyList<TranscriptionFile>>(files);
    }

    /// <inheritdoc/>
    public Task<string> LoadSummaryAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TranscriptionFile> RenameSummaryAsync(string oldFilePath, string newName, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(oldFilePath)!;
        var newFileName = newName.Trim() + ".md";
        var newFilePath = Path.Combine(directory, newFileName);

        File.Move(oldFilePath, newFilePath);

        var info = new FileInfo(newFilePath);
        return Task.FromResult(new TranscriptionFile(
            DisplayName: FormatDisplayName(info.Name, info.LastWriteTime),
            FilePath: info.FullName,
            LastModified: info.LastWriteTime));
    }

    /// <summary>
    /// Converts a filename such as <c>20240315 1430.md</c> into a friendly label
    /// like <c>Mar 15, 2024 14:30</c>.  Falls back to the bare filename stem on
    /// any parse error so the UI always has something to display.
    /// </summary>
    private static string FormatDisplayName(string fileName, DateTime fallback)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (DateTime.TryParseExact(stem, "yyyyMMdd HHmm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt.ToString("MMM d, yyyy HH:mm");
        }

        return stem;
    }
}
