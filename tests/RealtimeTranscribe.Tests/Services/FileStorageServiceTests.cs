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
}
