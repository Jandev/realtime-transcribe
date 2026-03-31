using RealtimeTranscribe.Models;
using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TranscriptFormatter"/> — covers parsing and formatting logic,
/// edge cases, and the single-speaker fallback behaviour.
/// </summary>
public class TranscriptFormatterTests
{
    // -----------------------------------------------------------------------------------------
    // Parse — happy-path tests
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Parse_SingleSpeakerSegment_ReturnsSingleItem()
    {
        var result = TranscriptFormatter.Parse("Speaker 1: Hello, how is everyone?");

        Assert.Single(result);
        Assert.Equal("Speaker 1", result[0].SpeakerId);
        Assert.Equal("Hello, how is everyone?", result[0].Text);
    }

    [Fact]
    public void Parse_TwoSpeakers_ReturnsCorrectSegments()
    {
        var input = """
            Speaker 1: Good morning everyone.
            Speaker 2: Good morning! Ready to start?
            """;

        var result = TranscriptFormatter.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("Speaker 1", result[0].SpeakerId);
        Assert.Equal("Good morning everyone.", result[0].Text);
        Assert.Equal("Speaker 2", result[1].SpeakerId);
        Assert.Equal("Good morning! Ready to start?", result[1].Text);
    }

    [Fact]
    public void Parse_MultipleSpeakersWithRapidSwitches_ReturnsAllSegments()
    {
        var input = """
            Speaker 1: Let's discuss the budget.
            Speaker 2: I agree we need to review it.
            Speaker 1: Q3 figures are available.
            Speaker 3: I have the report here.
            Speaker 2: Great, let's proceed.
            """;

        var result = TranscriptFormatter.Parse(input);

        Assert.Equal(5, result.Count);
        Assert.Equal("Speaker 1", result[0].SpeakerId);
        Assert.Equal("Speaker 2", result[1].SpeakerId);
        Assert.Equal("Speaker 1", result[2].SpeakerId);
        Assert.Equal("Speaker 3", result[3].SpeakerId);
        Assert.Equal("Speaker 2", result[4].SpeakerId);
    }

    [Fact]
    public void Parse_SpeakerWithMultiDigitNumber_ParsesCorrectly()
    {
        var result = TranscriptFormatter.Parse("Speaker 10: This is speaker ten.");

        Assert.Single(result);
        Assert.Equal("Speaker 10", result[0].SpeakerId);
        Assert.Equal("This is speaker ten.", result[0].Text);
    }

    [Fact]
    public void Parse_ContinuationLineAppendsToCurrentSegment()
    {
        var input = "Speaker 1: First line of speech.\nThis continues the same turn.";

        var result = TranscriptFormatter.Parse(input);

        Assert.Single(result);
        Assert.Equal("Speaker 1", result[0].SpeakerId);
        Assert.Contains("First line of speech.", result[0].Text);
        Assert.Contains("This continues the same turn.", result[0].Text);
    }

    // -----------------------------------------------------------------------------------------
    // Parse — fallback / edge-case tests
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Parse_NullInput_ReturnsEmptyList()
    {
        var result = TranscriptFormatter.Parse(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = TranscriptFormatter.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = TranscriptFormatter.Parse("   \n  \t  ");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NoSpeakerMarkers_ReturnsFallbackSingleSpeaker()
    {
        // When the GPT response doesn't include speaker markers, the entire text should be
        // wrapped in a single "Speaker 1" fallback segment.
        var input = "This is a transcript without any speaker markers at all.";

        var result = TranscriptFormatter.Parse(input);

        Assert.Single(result);
        Assert.Equal("Speaker 1", result[0].SpeakerId);
        Assert.Equal(input.Trim(), result[0].Text);
    }

    [Fact]
    public void Parse_PartiallyMissingMarkers_FallsBackToSingleSpeaker()
    {
        // Lines without "Speaker N:" prefix that appear *before* any speaker marker
        // result in the entire input being treated as single-speaker fallback text,
        // because no segments can be built without an initial marker.
        var input = "Just text without any speaker label.";

        var result = TranscriptFormatter.Parse(input);

        Assert.Single(result);
        Assert.Equal("Speaker 1", result[0].SpeakerId);
    }

    // -----------------------------------------------------------------------------------------
    // Format tests
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Format_EmptyList_ReturnsEmptyString()
    {
        var result = TranscriptFormatter.Format([]);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_SingleSegment_ReturnsSingleLine()
    {
        var segments = new List<SpeakerSegment>
        {
            new("Speaker 1", "Hello world.")
        };

        var result = TranscriptFormatter.Format(segments);

        Assert.Equal("Speaker 1: Hello world.", result);
    }

    [Fact]
    public void Format_MultipleSegments_ReturnsNewlineSeparatedLines()
    {
        var segments = new List<SpeakerSegment>
        {
            new("Speaker 1", "Good morning."),
            new("Speaker 2", "Morning! Ready?"),
            new("Speaker 1", "Let's start.")
        };

        var result = TranscriptFormatter.Format(segments);

        var lines = result.Split(Environment.NewLine);
        Assert.Equal(3, lines.Length);
        Assert.Equal("Speaker 1: Good morning.", lines[0]);
        Assert.Equal("Speaker 2: Morning! Ready?", lines[1]);
        Assert.Equal("Speaker 1: Let's start.", lines[2]);
    }

    // -----------------------------------------------------------------------------------------
    // Round-trip test
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ParseThenFormat_ProducesIdenticalOutput()
    {
        var original = string.Join(Environment.NewLine,
            "Speaker 1: Let's review the agenda.",
            "Speaker 2: Sure, I have it here.",
            "Speaker 1: Great. First item: budget.");

        var parsed = TranscriptFormatter.Parse(original);
        var formatted = TranscriptFormatter.Format(parsed);

        Assert.Equal(original, formatted);
    }
}
