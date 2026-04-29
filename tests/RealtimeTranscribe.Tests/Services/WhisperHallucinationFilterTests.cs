using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="WhisperHallucinationFilter"/>.
/// Covers the canonical hallucination phrases the user reported plus the multi-line
/// and repetition-collapse edge cases.
/// </summary>
public class WhisperHallucinationFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    public void Filter_ReturnsEmpty_ForEmptyInput(string? input)
    {
        Assert.Equal(string.Empty, WhisperHallucinationFilter.Filter(input));
    }

    [Theory]
    // The exact hallucinations the user reported.
    [InlineData("Thank you for watching.")]
    [InlineData("Thank you for watching")]
    [InlineData("THANK YOU FOR WATCHING!")]
    [InlineData("ご視聴ありがとうございました")]
    [InlineData("ご視聴ありがとうございました。")]
    // Other well-known canonical hallucinations.
    [InlineData("Thanks for watching!")]
    [InlineData("Bedankt voor het kijken.")]
    [InlineData("感谢观看")]
    [InlineData("you")]
    public void Filter_ReturnsEmpty_ForKnownHallucination(string input)
    {
        Assert.Equal(string.Empty, WhisperHallucinationFilter.Filter(input));
    }

    [Fact]
    public void Filter_PreservesRealSpeech()
    {
        const string speech = "Let's review the deployment plan for next quarter.";
        Assert.Equal(speech, WhisperHallucinationFilter.Filter(speech));
    }

    [Fact]
    public void Filter_RemovesTrailingHallucinationLine()
    {
        // Real speech followed by the hallucination on its own line at the end.
        string input = "We agreed on the new pricing.\nThank you for watching.";
        string expected = "We agreed on the new pricing.";
        Assert.Equal(expected, WhisperHallucinationFilter.Filter(input));
    }

    [Fact]
    public void Filter_RemovesLeadingHallucinationLine()
    {
        string input = "ご視聴ありがとうございました\nThe quarterly numbers look strong.";
        string expected = "The quarterly numbers look strong.";
        Assert.Equal(expected, WhisperHallucinationFilter.Filter(input));
    }

    [Fact]
    public void Filter_CollapsesTriviallyRepeatingTokens()
    {
        // The "混ぜる 混ぜる 混ぜる …" failure mode the user reported.  Once Whisper enters
        // this state the entire chunk is unreliable, so we drop it wholesale.
        string input = "混ぜる 混ぜる 混ぜる 混ぜる 混ぜる 混ぜる 混ぜる 混ぜる";
        Assert.Equal(string.Empty, WhisperHallucinationFilter.Filter(input));
    }

    [Fact]
    public void Filter_DoesNotCollapseLegitimateRepetition()
    {
        // A natural sentence with a few repeated words must NOT be classified as a
        // hallucination — only absurd repetition (5+ of the same token) is.
        const string input = "We need more tests, more docs, and more reviewers.";
        Assert.Equal(input, WhisperHallucinationFilter.Filter(input));
    }
}
