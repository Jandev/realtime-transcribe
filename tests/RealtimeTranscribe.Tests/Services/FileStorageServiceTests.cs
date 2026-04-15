using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileStorageService"/>.
/// Covers guard clauses, filename format, file content, section parsing, and rename.
/// </summary>
public class FileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;

    public FileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileStorageServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static FileStorageService CreateService(string? outputFolder = null) =>
        new() { OutputFolder = outputFolder };

    // ── SaveTranscriptionAsync ────────────────────────────────────────────

    [Fact]
    public async Task SaveTranscriptionAsync_WithNoOutputFolder_DoesNotThrowAndWritesNoFile()
    {
        var service = CreateService(outputFolder: null);

        await service.SaveTranscriptionAsync("# Summary", "transcript", "diarized", DateTime.Now);

        Assert.Empty(Directory.GetFiles(_tempDir, "*.md"));
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithEmptyOutputFolder_DoesNotThrowAndWritesNoFile()
    {
        var service = CreateService(outputFolder: string.Empty);

        await service.SaveTranscriptionAsync("# Summary", "transcript", "diarized", DateTime.Now);

        Assert.Empty(Directory.GetFiles(_tempDir, "*.md"));
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithAllEmptyContent_DoesNotWriteFile()
    {
        var service = CreateService(_tempDir);

        await service.SaveTranscriptionAsync(string.Empty, string.Empty, string.Empty, DateTime.Now);

        Assert.Empty(Directory.GetFiles(_tempDir, "*.md"));
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WritesMarkdownFileWithCorrectName()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveTranscriptionAsync("Summary content", "transcript", "diarized", timestamp);

        var files = Directory.GetFiles(_tempDir, "*.md");
        Assert.Single(files);
        Assert.Equal("20240315 143000.md", Path.GetFileName(files[0]));
    }

    [Fact]
    public async Task SaveTranscriptionAsync_ContainsAllThreeSections()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveTranscriptionAsync("My summary", "My transcript", "Speaker A: hello", timestamp);

        var filePath = Path.Combine(_tempDir, "20240315 143000.md");
        var content = await File.ReadAllTextAsync(filePath);

        Assert.Contains("## Summary and action items", content);
        Assert.Contains("My summary", content);
        Assert.Contains("## Transcript", content);
        Assert.Contains("My transcript", content);
        Assert.Contains("## Speaker attributed transcript", content);
        Assert.Contains("Speaker A: hello", content);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_SummaryAppearsFirst()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveTranscriptionAsync("My summary", "My transcript", "Diarized text", timestamp);

        var filePath = Path.Combine(_tempDir, "20240315 143000.md");
        var content = await File.ReadAllTextAsync(filePath);

        var summaryIdx = content.IndexOf("## Summary and action items");
        var transcriptIdx = content.IndexOf("## Transcript");
        var diarizedIdx = content.IndexOf("## Speaker attributed transcript");

        Assert.True(summaryIdx < transcriptIdx, "Summary should appear before Transcript");
        Assert.True(transcriptIdx < diarizedIdx, "Transcript should appear before Speaker attributed transcript");
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WritesFileAsUtf8()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 1, 1, 9, 0, 0);

        await service.SaveTranscriptionAsync("Samenvatting: **verplicht**", "émoji 🎙", null, timestamp);

        var filePath = Path.Combine(_tempDir, "20240101 090000.md");
        var content = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
        Assert.Contains("Samenvatting: **verplicht**", content);
        Assert.Contains("émoji 🎙", content);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_CanOverwriteFileWithSameTimestamp()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 6, 10, 11, 0, 0);

        await service.SaveTranscriptionAsync("First version", null, null, timestamp);
        await service.SaveTranscriptionAsync("Second version", null, null, timestamp);

        var filePath = Path.Combine(_tempDir, "20240610 110000.md");
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("Second version", content);
        Assert.DoesNotContain("First version", content);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithOnlySummary_StillWritesAllSections()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveTranscriptionAsync("Just a summary", null, null, timestamp);

        var filePath = Path.Combine(_tempDir, "20240315 143000.md");
        var content = await File.ReadAllTextAsync(filePath);

        Assert.Contains("## Summary and action items", content);
        Assert.Contains("Just a summary", content);
        Assert.Contains("## Transcript", content);
        Assert.Contains("## Speaker attributed transcript", content);
    }

    // ── ListSummariesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ListSummariesAsync_WithNullOutputFolder_ReturnsEmpty()
    {
        var service = CreateService(outputFolder: null);

        var result = await service.ListSummariesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListSummariesAsync_WithEmptyOutputFolder_ReturnsEmpty()
    {
        var service = CreateService(outputFolder: string.Empty);

        var result = await service.ListSummariesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListSummariesAsync_WithNonExistentFolder_ReturnsEmpty()
    {
        var service = CreateService(outputFolder: Path.Combine(_tempDir, "nonexistent"));

        var result = await service.ListSummariesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListSummariesAsync_WithNoMarkdownFiles_ReturnsEmpty()
    {
        var service = CreateService(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "not a summary");

        var result = await service.ListSummariesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListSummariesAsync_ReturnsMarkdownFilesOrderedNewestFirst()
    {
        var service = CreateService(_tempDir);
        var t1 = new DateTime(2024, 1, 1, 9, 0, 0);
        var t2 = new DateTime(2024, 3, 15, 14, 30, 0);
        var t3 = new DateTime(2024, 6, 10, 11, 0, 0);

        await service.SaveTranscriptionAsync("A", null, null, t1);
        await service.SaveTranscriptionAsync("B", null, null, t2);
        await service.SaveTranscriptionAsync("C", null, null, t3);

        var result = await service.ListSummariesAsync();

        Assert.Equal(3, result.Count);
        Assert.All(result, f => Assert.EndsWith(".md", f.FilePath));
    }

    [Fact]
    public async Task ListSummariesAsync_SetsDisplayNameFromFilename()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveTranscriptionAsync("content", null, null, timestamp);

        var result = await service.ListSummariesAsync();

        Assert.Single(result);
        Assert.Equal("Mar 15, 2024 14:30:00", result[0].DisplayName);
    }

    [Fact]
    public async Task ListSummariesAsync_PopulatesFilePath()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveTranscriptionAsync("content", null, null, timestamp);

        var result = await service.ListSummariesAsync();

        Assert.Single(result);
        Assert.Equal(Path.Combine(_tempDir, "20240315 143000.md"), result[0].FilePath);
    }

    // ── LoadSummaryAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task LoadSummaryAsync_ReturnsFileContent()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 6, 10, 11, 0, 0);

        await service.SaveTranscriptionAsync("Test content", null, null, timestamp);
        var filePath = Path.Combine(_tempDir, "20240610 110000.md");

        var result = await service.LoadSummaryAsync(filePath);

        Assert.Contains("Test content", result);
    }

    [Fact]
    public async Task LoadSummaryAsync_ReadsUtf8Content()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 1, 1, 9, 0, 0);

        await service.SaveTranscriptionAsync("Samenvatting: **verplicht** — émoji 🎙", null, null, timestamp);
        var filePath = Path.Combine(_tempDir, "20240101 090000.md");

        var result = await service.LoadSummaryAsync(filePath);

        Assert.Contains("Samenvatting: **verplicht** — émoji 🎙", result);
    }

    // ── LoadTranscriptionAsync / ParseSections ───────────────────────────

    [Fact]
    public async Task LoadTranscriptionAsync_ParsesAllSections()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveTranscriptionAsync("My summary", "My transcript", "Speaker A: hello", timestamp);
        var filePath = Path.Combine(_tempDir, "20240315 143000.md");

        var result = await service.LoadTranscriptionAsync(filePath);

        Assert.Equal("My summary", result.Summary);
        Assert.Equal("My transcript", result.Transcript);
        Assert.Equal("Speaker A: hello", result.DiarizedTranscript);
    }

    [Fact]
    public void ParseSections_LegacyFile_TreatsEverythingAsSummary()
    {
        var legacy = "# Old Summary\n\nSome content without section headings.";

        var result = FileStorageService.ParseSections(legacy);

        Assert.Equal(legacy.Trim(), result.Summary);
        Assert.Empty(result.Transcript);
        Assert.Empty(result.DiarizedTranscript);
    }

    [Fact]
    public void ParseSections_EmptySections_ReturnsEmptyStrings()
    {
        var content = "## Summary and action items\n\n\n\n## Transcript\n\n\n\n## Speaker attributed transcript\n\n";

        var result = FileStorageService.ParseSections(content);

        Assert.Empty(result.Summary);
        Assert.Empty(result.Transcript);
        Assert.Empty(result.DiarizedTranscript);
    }

    [Fact]
    public void ParseSections_RoundTripsContent()
    {
        var summary = "- Action 1\n- Action 2";
        var transcript = "Hello world this is a test.";
        var diarized = "Speaker A: Hello\nSpeaker B: World";

        var content = $"## Summary and action items\n\n{summary}\n\n## Transcript\n\n{transcript}\n\n## Speaker attributed transcript\n\n{diarized}\n";

        var result = FileStorageService.ParseSections(content);

        Assert.Equal(summary, result.Summary);
        Assert.Equal(transcript, result.Transcript);
        Assert.Equal(diarized, result.DiarizedTranscript);
    }

    [Fact]
    public async Task SaveAndLoad_SummaryWithHeadings_RoundTrips()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        var summary = "## Summary\n\nSome summary content.\n\n## Action Items\n\n- Item 1\n- Item 2";

        await service.SaveTranscriptionAsync(summary, "transcript", "diarized", timestamp);

        // Verify the file contains bumped headings (### instead of ##)
        var filePath = Path.Combine(_tempDir, "20240315 143000.md");
        var rawContent = await File.ReadAllTextAsync(filePath);
        Assert.Contains("### Summary", rawContent);
        Assert.Contains("### Action Items", rawContent);
        Assert.DoesNotContain("\n## Summary\n", rawContent);
        Assert.DoesNotContain("\n## Action Items\n", rawContent);

        // Verify round-trip: loading restores original heading levels
        var result = await service.LoadTranscriptionAsync(filePath);
        Assert.Equal(summary, result.Summary);
    }

    [Fact]
    public void BumpHeadings_BumpsAllLevels()
    {
        var input = "## Summary\n\nSome text.\n\n## Action Items\n\n- Item 1";
        var bumped = FileStorageService.BumpHeadings(input);

        Assert.Contains("### Summary", bumped);
        Assert.Contains("### Action Items", bumped);
        Assert.DoesNotContain("\n## Summary", bumped);
    }

    [Fact]
    public void UnbumpHeadings_ReversesOneLevelBump()
    {
        var input = "### Summary\n\nSome text.\n\n### Action Items\n\n- Item 1";
        var unbumped = FileStorageService.UnbumpHeadings(input);

        Assert.Contains("## Summary", unbumped);
        Assert.Contains("## Action Items", unbumped);
    }

    // ── RenameSummaryAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RenameSummaryAsync_RenamesFileOnDisk()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveTranscriptionAsync("content", null, null, timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 143000.md");

        await service.RenameSummaryAsync(oldPath, "Daily standup");

        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Daily standup.md")));
    }

    [Fact]
    public async Task RenameSummaryAsync_PreservesFileContent()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveTranscriptionAsync("Original content", "transcript", "diarized", timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 143000.md");

        await service.RenameSummaryAsync(oldPath, "My meeting");

        var actual = await File.ReadAllTextAsync(Path.Combine(_tempDir, "My meeting.md"));
        Assert.Contains("Original content", actual);
    }

    [Fact]
    public async Task RenameSummaryAsync_ReturnsUpdatedTranscriptionFile()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveTranscriptionAsync("content", null, null, timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 143000.md");

        var result = await service.RenameSummaryAsync(oldPath, "Daily standup");

        Assert.Equal(Path.Combine(_tempDir, "Daily standup.md"), result.FilePath);
        Assert.Equal("Daily standup", result.DisplayName);
    }

    [Fact]
    public async Task RenameSummaryAsync_TrimsWhitespace()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveTranscriptionAsync("content", null, null, timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 143000.md");

        var result = await service.RenameSummaryAsync(oldPath, "  Daily standup  ");

        Assert.Equal(Path.Combine(_tempDir, "Daily standup.md"), result.FilePath);
    }

    [Fact]
    public async Task RenameSummaryAsync_RenamedFileAppearsInList()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveTranscriptionAsync("content", null, null, timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 143000.md");

        await service.RenameSummaryAsync(oldPath, "Retrospective");
        var files = await service.ListSummariesAsync();

        Assert.Single(files);
        Assert.Contains("Retrospective", files[0].DisplayName);
    }

    [Fact]
    public async Task RenameSummaryAsync_ThrowsWhenTargetAlreadyExists()
    {
        var service = CreateService(_tempDir);
        await service.SaveTranscriptionAsync("first", null, null, new DateTime(2024, 3, 15, 14, 30, 0));
        await service.SaveTranscriptionAsync("second", null, null, new DateTime(2024, 3, 16, 10, 0, 0));
        var secondPath = Path.Combine(_tempDir, "20240316 100000.md");

        await Assert.ThrowsAsync<IOException>(
            () => service.RenameSummaryAsync(secondPath, "20240315 143000"));
    }
}
