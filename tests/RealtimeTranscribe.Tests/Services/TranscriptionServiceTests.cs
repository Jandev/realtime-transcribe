using RealtimeTranscribe.Models;
using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TranscriptionService"/> guard clauses.
/// These tests exercise logic paths that return before any network call is made,
/// so no Azure credentials are required.
/// </summary>
public class TranscriptionServiceTests
{
    private static TranscriptionService CreateService() =>
        new(new AzureOpenAISettings { Endpoint = "https://fake.openai.azure.com/" });

    [Fact]
    public async Task TranscribeAsync_WithEmptyByteArray_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.TranscribeAsync(Array.Empty<byte>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SummarizeAsync_WithEmptyTranscript_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.SummarizeAsync(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SummarizeAsync_WithWhitespaceTranscript_ReturnsEmptyString()
    {
        var service = CreateService();

        var result = await service.SummarizeAsync("   ");

        Assert.Equal(string.Empty, result);
    }
}
