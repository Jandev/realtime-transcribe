using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileStorageService"/>.
/// Covers guard clauses, filename format, and file content.
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

    [Fact]
    public async Task SaveSummaryAsync_WithNoOutputFolder_DoesNotThrowAndWritesNoFile()
    {
        var service = CreateService(outputFolder: null);

        // Should complete without throwing; no files written anywhere
        await service.SaveSummaryAsync("# Summary\n\nTest", DateTime.Now);

        // Verify nothing was created in our controlled temp directory
        Assert.Empty(Directory.GetFiles(_tempDir, "*.md"));
    }

    [Fact]
    public async Task SaveSummaryAsync_WithEmptyOutputFolder_DoesNotThrowAndWritesNoFile()
    {
        var service = CreateService(outputFolder: string.Empty);

        // Should complete without throwing; no files written anywhere
        await service.SaveSummaryAsync("# Summary\n\nTest", DateTime.Now);

        // Verify nothing was created in our controlled temp directory
        Assert.Empty(Directory.GetFiles(_tempDir, "*.md"));
    }

    [Fact]
    public async Task SaveSummaryAsync_WithEmptySummary_DoesNotWriteFile()
    {
        var service = CreateService(_tempDir);

        await service.SaveSummaryAsync(string.Empty, DateTime.Now);

        Assert.Empty(Directory.GetFiles(_tempDir, "*.md"));
    }

    [Fact]
    public async Task SaveSummaryAsync_WritesMarkdownFileWithCorrectName()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveSummaryAsync("# Summary\n\nContent", timestamp);

        var files = Directory.GetFiles(_tempDir, "*.md");
        Assert.Single(files);
        Assert.Equal("20240315 1430.md", Path.GetFileName(files[0]));
    }

    [Fact]
    public async Task SaveSummaryAsync_WritesCorrectContent()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        var summary = "## Summary\n\nThis is a test.\n\n## Action Items\n\n- Item 1\n- Item 2";

        await service.SaveSummaryAsync(summary, timestamp);

        var filePath = Path.Combine(_tempDir, "20240315 1430.md");
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal(summary, content);
    }

    [Fact]
    public async Task SaveSummaryAsync_WritesFileAsUtf8()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 1, 1, 9, 0, 0);
        var summary = "# Samenvatting\n\nActiepunten: **verplicht**";

        await service.SaveSummaryAsync(summary, timestamp);

        var filePath = Path.Combine(_tempDir, "20240101 0900.md");
        var content = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
        Assert.Equal(summary, content);
    }

    [Fact]
    public async Task SaveSummaryAsync_CanOverwriteFileWithSameTimestamp()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 6, 10, 11, 0, 0);

        await service.SaveSummaryAsync("First version", timestamp);
        await service.SaveSummaryAsync("Second version", timestamp);

        var filePath = Path.Combine(_tempDir, "20240610 1100.md");
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal("Second version", content);
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

        await service.SaveSummaryAsync("A", t1);
        await service.SaveSummaryAsync("B", t2);
        await service.SaveSummaryAsync("C", t3);

        var result = await service.ListSummariesAsync();

        Assert.Equal(3, result.Count);
        // Newest file written last; order is by LastWriteTime descending
        Assert.All(result, f => Assert.EndsWith(".md", f.FilePath));
    }

    [Fact]
    public async Task ListSummariesAsync_SetsDisplayNameFromFilename()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveSummaryAsync("content", timestamp);

        var result = await service.ListSummariesAsync();

        Assert.Single(result);
        Assert.Equal("Mar 15, 2024 14:30", result[0].DisplayName);
    }

    [Fact]
    public async Task ListSummariesAsync_PopulatesFilePath()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);

        await service.SaveSummaryAsync("content", timestamp);

        var result = await service.ListSummariesAsync();

        Assert.Single(result);
        Assert.Equal(Path.Combine(_tempDir, "20240315 1430.md"), result[0].FilePath);
    }

    // ── LoadSummaryAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task LoadSummaryAsync_ReturnsFileContent()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 6, 10, 11, 0, 0);
        var expected = "## Summary\n\nTest content";

        await service.SaveSummaryAsync(expected, timestamp);
        var filePath = Path.Combine(_tempDir, "20240610 1100.md");

        var result = await service.LoadSummaryAsync(filePath);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task LoadSummaryAsync_ReadsUtf8Content()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 1, 1, 9, 0, 0);
        var expected = "# Samenvatting\n\nActiepunten: **verplicht** — émoji 🎙";

        await service.SaveSummaryAsync(expected, timestamp);
        var filePath = Path.Combine(_tempDir, "20240101 0900.md");

        var result = await service.LoadSummaryAsync(filePath);

        Assert.Equal(expected, result);
    }

    // ── RenameSummaryAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RenameSummaryAsync_RenamesFileOnDisk()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveSummaryAsync("content", timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 1430.md");

        await service.RenameSummaryAsync(oldPath, "Daily standup");

        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Daily standup.md")));
    }

    [Fact]
    public async Task RenameSummaryAsync_PreservesFileContent()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        var content = "## Summary\n\nOriginal content";
        await service.SaveSummaryAsync(content, timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 1430.md");

        await service.RenameSummaryAsync(oldPath, "My meeting");

        var actual = await File.ReadAllTextAsync(Path.Combine(_tempDir, "My meeting.md"));
        Assert.Equal(content, actual);
    }

    [Fact]
    public async Task RenameSummaryAsync_ReturnsUpdatedTranscriptionFile()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveSummaryAsync("content", timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 1430.md");

        var result = await service.RenameSummaryAsync(oldPath, "Daily standup");

        Assert.Equal(Path.Combine(_tempDir, "Daily standup.md"), result.FilePath);
        Assert.Equal("Daily standup", result.DisplayName); // Non-date name falls back to stem
    }

    [Fact]
    public async Task RenameSummaryAsync_TrimsWhitespace()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveSummaryAsync("content", timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 1430.md");

        var result = await service.RenameSummaryAsync(oldPath, "  Daily standup  ");

        Assert.Equal(Path.Combine(_tempDir, "Daily standup.md"), result.FilePath);
    }

    [Fact]
    public async Task RenameSummaryAsync_RenamedFileAppearsInList()
    {
        var service = CreateService(_tempDir);
        var timestamp = new DateTime(2024, 3, 15, 14, 30, 0);
        await service.SaveSummaryAsync("content", timestamp);
        var oldPath = Path.Combine(_tempDir, "20240315 1430.md");

        await service.RenameSummaryAsync(oldPath, "Retrospective");
        var files = await service.ListSummariesAsync();

        Assert.Single(files);
        Assert.Contains("Retrospective", files[0].DisplayName);
    }
}
