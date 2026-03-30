using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TranscriptionScheduler"/>.
/// Uses in-memory test doubles to avoid any Azure dependency.
/// </summary>
public class TranscriptionSchedulerTests
{
    // ---------------------------------------------------------------------------
    // Test doubles
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Transcription service stub that returns a configurable transcript
    /// or throws a configurable exception.
    /// </summary>
    private sealed class StubTranscriptionService : ITranscriptionService
    {
        private readonly string _transcript;
        private readonly Exception? _throwOnTranscribe;

        public int TranscribeCallCount { get; private set; }

        public StubTranscriptionService(string transcript = "segment", Exception? throwOnTranscribe = null)
        {
            _transcript = transcript;
            _throwOnTranscribe = throwOnTranscribe;
        }

        public Task<string> TranscribeAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
        {
            TranscribeCallCount++;
            if (_throwOnTranscribe is not null)
                throw _throwOnTranscribe;
            return Task.FromResult(wavBytes.Length == 0 ? string.Empty : _transcript);
        }

        public Task<string> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static readonly byte[] FakeAudioChunk = new byte[] { 1, 2, 3 };
    private static readonly byte[] EmptyChunk = Array.Empty<byte>();

    private static Func<CancellationToken, Task<byte[]>> FixedChunkProvider(byte[] chunk)
        => _ => Task.FromResult(chunk);

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WhenCancelledImmediately_DoesNotCallOnSegment()
    {
        var service = new StubTranscriptionService();
        var scheduler = new TranscriptionScheduler(service, TimeSpan.FromMilliseconds(50));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancelled before the loop even starts

        await scheduler.RunAsync(
            FixedChunkProvider(FakeAudioChunk),
            segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cts.Token);

        Assert.Empty(segmentsReceived);
    }

    [Fact]
    public async Task RunAsync_WithEmptyChunk_DoesNotCallOnSegment()
    {
        var service = new StubTranscriptionService();
        var scheduler = new TranscriptionScheduler(service, TimeSpan.FromMilliseconds(30));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(120));

        await scheduler.RunAsync(
            FixedChunkProvider(EmptyChunk),
            segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cts.Token);

        Assert.Empty(segmentsReceived);
        Assert.Equal(0, service.TranscribeCallCount);
    }

    [Fact]
    public async Task RunAsync_WithChunkAvailable_CallsOnSegmentAtLeastOnce()
    {
        var service = new StubTranscriptionService("hello world");
        var scheduler = new TranscriptionScheduler(service, TimeSpan.FromMilliseconds(30));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(120));

        await scheduler.RunAsync(
            FixedChunkProvider(FakeAudioChunk),
            segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cts.Token);

        Assert.NotEmpty(segmentsReceived);
        Assert.All(segmentsReceived, s => Assert.Equal("hello world", s));
    }

    [Fact]
    public async Task RunAsync_WithShortInterval_CallsOnSegmentMultipleTimes()
    {
        var service = new StubTranscriptionService("chunk");
        var scheduler = new TranscriptionScheduler(service, TimeSpan.FromMilliseconds(30));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await scheduler.RunAsync(
            FixedChunkProvider(FakeAudioChunk),
            segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cts.Token);

        // With a 30 ms interval and a 200 ms window we expect at least 2 segments.
        Assert.True(segmentsReceived.Count >= 2,
            $"Expected at least 2 segments but got {segmentsReceived.Count}.");
    }

    [Fact]
    public async Task RunAsync_WhenTranscriptionThrows_ContinuesLoop()
    {
        // Throws on the first two calls, succeeds on the third.
        var fakeService = new FaultingTranscriptionService(failuresBeforeSuccess: 2, successTranscript: "recovered");
        var scheduler = new TranscriptionScheduler(fakeService, TimeSpan.FromMilliseconds(30));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await scheduler.RunAsync(
            FixedChunkProvider(FakeAudioChunk),
            segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cts.Token);

        // Loop should have recovered and eventually produced at least one segment.
        Assert.Contains("recovered", segmentsReceived);
    }

    [Fact]
    public async Task RunAsync_WhenAudioProviderThrows_ContinuesLoop()
    {
        int providerCalls = 0;
        var service = new StubTranscriptionService("ok");
        var scheduler = new TranscriptionScheduler(service, TimeSpan.FromMilliseconds(30));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await scheduler.RunAsync(
            audioChunkProvider: ct =>
            {
                providerCalls++;
                // Throw on the first call; return a chunk on subsequent calls.
                if (providerCalls == 1)
                    throw new InvalidOperationException("audio error");
                return Task.FromResult(FakeAudioChunk);
            },
            onSegment: segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cancellationToken: cts.Token);

        // The first cycle fails (audio error), but the loop recovers.
        Assert.True(providerCalls >= 2, $"Expected at least 2 provider calls but got {providerCalls}.");
        Assert.NotEmpty(segmentsReceived);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringDelay_ReturnsProperly()
    {
        // Use a very long interval to guarantee the cancellation fires while waiting.
        var service = new StubTranscriptionService();
        var scheduler = new TranscriptionScheduler(service, TimeSpan.FromSeconds(60));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Should complete quickly (not wait 60 s) when cancelled.
        await scheduler.RunAsync(
            FixedChunkProvider(FakeAudioChunk),
            segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cts.Token);

        Assert.Empty(segmentsReceived);
        Assert.Equal(0, service.TranscribeCallCount);
    }

    [Fact]
    public async Task RunAsync_AggregatesMultipleSegmentsInOrder()
    {
        string[] transcripts = { "first", "second", "third" };
        var service = new SequentialTranscriptionService(transcripts);
        var scheduler = new TranscriptionScheduler(service, TimeSpan.FromMilliseconds(20));

        var segmentsReceived = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(120));

        await scheduler.RunAsync(
            FixedChunkProvider(FakeAudioChunk),
            segment => { segmentsReceived.Add(segment); return Task.CompletedTask; },
            cts.Token);

        // Verify ordering is preserved.
        for (int i = 0; i < segmentsReceived.Count; i++)
        {
            Assert.Equal(transcripts[i % transcripts.Length], segmentsReceived[i]);
        }
    }

    [Fact]
    public void DefaultInterval_IsThirtySeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), TranscriptionScheduler.DefaultInterval);
    }

    // ---------------------------------------------------------------------------
    // Additional test doubles used in specific tests
    // ---------------------------------------------------------------------------

    private sealed class FaultingTranscriptionService : ITranscriptionService
    {
        private readonly int _failuresBeforeSuccess;
        private readonly string _successTranscript;
        private int _callCount;

        public FaultingTranscriptionService(int failuresBeforeSuccess, string successTranscript)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
            _successTranscript = successTranscript;
        }

        public Task<string> TranscribeAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
        {
            _callCount++;
            if (_callCount <= _failuresBeforeSuccess)
                throw new HttpRequestException("transient API error");
            return Task.FromResult(_successTranscript);
        }

        public Task<string> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class SequentialTranscriptionService : ITranscriptionService
    {
        private readonly string[] _transcripts;
        private int _index;

        public SequentialTranscriptionService(string[] transcripts)
        {
            _transcripts = transcripts;
        }

        public Task<string> TranscribeAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
        {
            var result = _transcripts[_index % _transcripts.Length];
            _index++;
            return Task.FromResult(wavBytes.Length == 0 ? string.Empty : result);
        }

        public Task<string> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }
}
