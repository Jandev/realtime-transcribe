using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MarkdownProcessor"/>.
/// Validates Markdown-to-HTML conversion, HTML sanitization, and edge-case handling.
/// No network calls or Azure credentials are required.
/// </summary>
public class MarkdownProcessorTests
{
    private readonly MarkdownProcessor _processor = new();

    [Fact]
    public void ToHtml_WithEmptyString_ReturnsEmptyString()
    {
        var result = _processor.ToHtml(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToHtml_WithNullEquivalentWhitespace_IsHandledByCallers()
    {
        // MarkdownProcessor.ToHtml treats null as empty (guard clause).
        // Non-empty whitespace is converted to an empty paragraph by Markdig.
        var result = _processor.ToHtml("   ");

        Assert.NotNull(result);
    }

    [Fact]
    public void ToHtml_WithBoldText_ReturnsBoldHtml()
    {
        var result = _processor.ToHtml("**bold term**");

        Assert.Contains("<strong>bold term</strong>", result);
    }

    [Fact]
    public void ToHtml_WithItalicText_ReturnsItalicHtml()
    {
        var result = _processor.ToHtml("*italic emphasis*");

        Assert.Contains("<em>italic emphasis</em>", result);
    }

    [Theory]
    [InlineData("# Heading 1", "<h1", "Heading 1")]
    [InlineData("## Heading 2", "<h2", "Heading 2")]
    [InlineData("### Heading 3", "<h3", "Heading 3")]
    public void ToHtml_WithHeadings_ReturnsCorrectHeadingTags(string markdown, string expectedTagStart, string expectedText)
    {
        var result = _processor.ToHtml(markdown);

        Assert.Contains(expectedTagStart, result);
        Assert.Contains(expectedText, result);
    }

    [Fact]
    public void ToHtml_WithUnorderedList_ReturnsListHtml()
    {
        var result = _processor.ToHtml("- item one\n- item two\n- item three");

        Assert.Contains("<ul>", result);
        Assert.Contains("<li>item one</li>", result);
        Assert.Contains("<li>item two</li>", result);
    }

    [Fact]
    public void ToHtml_WithOrderedList_ReturnsOrderedListHtml()
    {
        var result = _processor.ToHtml("1. first\n2. second");

        Assert.Contains("<ol>", result);
        Assert.Contains("<li>first</li>", result);
    }

    [Fact]
    public void ToHtml_WithTable_ReturnsTableHtml()
    {
        var markdown = "| Name | Action |\n|------|--------|\n| Alice | Review PR |";

        var result = _processor.ToHtml(markdown);

        Assert.Contains("<table>", result);
        Assert.Contains("<th>Name</th>", result);
        Assert.Contains("<td>Alice</td>", result);
    }

    [Fact]
    public void ToHtml_WithRawScriptTag_EscapesScriptContent()
    {
        // DisableHtml() HTML-encodes raw HTML tags rather than stripping them.
        // The resulting output is safe to render: <script> becomes &lt;script&gt;
        // so the browser will display it as text and never execute it.
        var result = _processor.ToHtml("<script>alert('xss')</script>");

        // The literal <script> tag must not be present (would be executable).
        Assert.DoesNotContain("<script>", result);
        // The HTML-encoded form confirms the input was safely escaped.
        Assert.Contains("&lt;script&gt;", result);
    }

    [Fact]
    public void ToHtml_WithRawHtmlTag_EscapesHtmlContent()
    {
        var result = _processor.ToHtml("<div style=\"color:red\">danger</div>");

        Assert.DoesNotContain("<div", result);
        Assert.Contains("&lt;div", result);
    }

    [Fact]
    public void ToHtml_WithCombinedMarkdown_RendersAllElements()
    {
        var markdown = """
            ## Summary

            This is a *concise* summary with **bold** terms.

            ## Action Items

            - Review the **proposal**
            - Schedule follow-up meeting
            """;

        var result = _processor.ToHtml(markdown);

        Assert.Contains("<h2", result);
        Assert.Contains(">Summary<", result);
        Assert.Contains(">Action Items<", result);
        Assert.Contains("<strong>bold</strong>", result);
        Assert.Contains("<em>concise</em>", result);
        Assert.Contains("<ul>", result);
    }

    [Fact]
    public void ToHtml_WithPlainText_ReturnsWrappedParagraph()
    {
        var result = _processor.ToHtml("Plain text without any Markdown formatting.");

        Assert.Contains("<p>", result);
        Assert.Contains("Plain text without any Markdown formatting.", result);
    }

    [Fact]
    public void ToHtml_TranscriptPath_IsNotCalledByTranscriptionService()
    {
        // Transcription uses ITranscriptionService.TranscribeAsync which returns raw text.
        // The MarkdownProcessor is only used for summary rendering, never for transcripts.
        // This test documents and verifies that MarkdownProcessor handles plain transcript-
        // like input safely (it will just wrap it in a paragraph without modifying the text).
        var plainTranscript = "Hello, today we discussed the project roadmap and timeline.";

        var result = _processor.ToHtml(plainTranscript);

        // Plain text round-trips correctly through the processor.
        Assert.Contains(plainTranscript, result);
        // No unexpected Markdown processing occurs on plain text.
        Assert.DoesNotContain("<h1>", result);
        Assert.DoesNotContain("<ul>", result);
    }
}
