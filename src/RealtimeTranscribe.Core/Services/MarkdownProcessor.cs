using Markdig;

namespace RealtimeTranscribe.Services;

/// <summary>
/// Converts Markdown to a sanitized HTML fragment using Markdig.
/// Raw HTML embedded in the Markdown input is disabled to prevent unsafe content rendering.
/// Supports headings, bold, italic, bullet lists, ordered lists, and tables.
/// </summary>
public class MarkdownProcessor : IMarkdownProcessor
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    /// <inheritdoc/>
    public string ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        return Markdown.ToHtml(markdown, Pipeline);
    }
}
