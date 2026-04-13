using System.Text;
using System.Text.RegularExpressions;
using RealtimeTranscribe.Models;

namespace RealtimeTranscribe.Services;

/// <inheritdoc cref="IFileStorageService"/>
public partial class FileStorageService : IFileStorageService
{
    // Section headers used when writing and parsing transcription files.
    private const string SummaryHeader = "## Summary and action items";
    private const string TranscriptHeader = "## Transcript";
    private const string DiarizedTranscriptHeader = "## Speaker attributed transcript";

    /// <inheritdoc/>
    public string? OutputFolder { get; set; }

    /// <inheritdoc/>
    public async Task SaveTranscriptionAsync(string? summary, string? transcript, string? diarizedTranscript, DateTime timestamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(OutputFolder))
            return;

        // At least one section must have content.
        if (string.IsNullOrEmpty(summary) && string.IsNullOrEmpty(transcript) && string.IsNullOrEmpty(diarizedTranscript))
            return;

        var sb = new StringBuilder();

        sb.AppendLine(SummaryHeader);
        sb.AppendLine();
        if (!string.IsNullOrEmpty(summary))
            sb.AppendLine(BumpHeadings(summary));
        sb.AppendLine();

        sb.AppendLine(TranscriptHeader);
        sb.AppendLine();
        if (!string.IsNullOrEmpty(transcript))
            sb.AppendLine(transcript);
        sb.AppendLine();

        sb.AppendLine(DiarizedTranscriptHeader);
        sb.AppendLine();
        if (!string.IsNullOrEmpty(diarizedTranscript))
            sb.AppendLine(diarizedTranscript);

        var fileName = timestamp.ToString("yyyyMMdd HHmm") + ".md";
        var filePath = Path.Combine(OutputFolder, fileName);

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, cancellationToken);
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
    public async Task<TranscriptionContent> LoadTranscriptionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ParseSections(content);
    }

    /// <summary>
    /// Parses the markdown content into sections.  If the file uses the new sectioned format
    /// (<c>## Summary and action items</c>, <c>## Transcript</c>, <c>## Speaker attributed transcript</c>)
    /// the content under each heading is returned in the corresponding property.
    /// For legacy files that lack these headings, the entire content is treated as the summary.
    /// </summary>
    public static TranscriptionContent ParseSections(string content)
    {
        // Split on ## headings.  Because the regex uses a capturing group,
        // the matched headings are interleaved with the body segments:
        //   [preamble, heading1, body1, heading2, body2, ...]
        var parts = SectionRegex().Split(content);

        // No recognised headings → legacy file, treat everything as summary.
        if (parts.Length <= 1)
            return new TranscriptionContent(Summary: content.Trim(), Transcript: string.Empty, DiarizedTranscript: string.Empty);

        string summary = string.Empty, transcript = string.Empty, diarized = string.Empty;

        // Walk pairs: parts[1]=heading, parts[2]=body, parts[3]=heading, parts[4]=body, …
        for (int i = 1; i + 1 < parts.Length; i += 2)
        {
            var heading = parts[i].Trim();
            var body = parts[i + 1].Trim();

            if (heading.Equals(SummaryHeader, StringComparison.OrdinalIgnoreCase))
                summary = UnbumpHeadings(body);
            else if (heading.Equals(TranscriptHeader, StringComparison.OrdinalIgnoreCase))
                transcript = body;
            else if (heading.Equals(DiarizedTranscriptHeader, StringComparison.OrdinalIgnoreCase))
                diarized = body;
        }

        return new TranscriptionContent(Summary: summary, Transcript: transcript, DiarizedTranscript: diarized);
    }

    /// <summary>Matches only the three known section heading lines used as delimiters.</summary>
    [GeneratedRegex(@"^(## Summary and action items|## Transcript|## Speaker attributed transcript)$", RegexOptions.Multiline)]
    private static partial Regex SectionRegex();

    /// <summary>Matches markdown headings (## or more) at the start of a line.</summary>
    [GeneratedRegex(@"^(#{2,})\s", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    /// <summary>
    /// Bumps all <c>##</c> (and deeper) headings in the text one level deeper so they
    /// nest properly under the <c>## Summary and action items</c> section header.
    /// For example, <c>## Summary</c> becomes <c>### Summary</c>.
    /// </summary>
    public static string BumpHeadings(string text) =>
        HeadingRegex().Replace(text, m => "#" + m.Value);

    /// <summary>
    /// Reverses <see cref="BumpHeadings"/>: removes one <c>#</c> from headings that are
    /// <c>###</c> or deeper, restoring the original heading levels.
    /// </summary>
    [GeneratedRegex(@"^(#{3,})\s", RegexOptions.Multiline)]
    private static partial Regex BumpedHeadingRegex();

    public static string UnbumpHeadings(string text) =>
        BumpedHeadingRegex().Replace(text, m => m.Groups[1].Value[1..] + " ");

    /// <inheritdoc/>
    public Task<TranscriptionFile> RenameSummaryAsync(string oldFilePath, string newName, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(oldFilePath)!;
        var newFileName = newName.Trim() + ".md";
        var newFilePath = Path.Combine(directory, newFileName);

        if (File.Exists(newFilePath))
            throw new IOException($"A file named '{newFileName}' already exists.");

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
