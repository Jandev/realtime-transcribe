using System.Text;

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
}
