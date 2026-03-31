namespace RealtimeTranscribe.Services;

/// <summary>
/// Converts Markdown text into sanitized HTML fragments.
/// Raw HTML embedded in the Markdown input is HTML-encoded (escaped) to prevent unsafe content rendering.
/// </summary>
public interface IMarkdownProcessor
{
    /// <summary>
    /// Converts a Markdown string to a sanitized HTML fragment.
    /// </summary>
    /// <param name="markdown">The Markdown input. Returns <see cref="string.Empty"/> when null or empty.</param>
    /// <returns>An HTML fragment representing the rendered Markdown.</returns>
    string ToHtml(string markdown);
}
