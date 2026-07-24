using System.Net;
using System.Text.RegularExpressions;

namespace Scrubkit;

/// <summary>
/// Clean text from HTML (.html / .htm): drops <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c>
/// blocks and comments, strips the remaining tags, decodes entities, and collapses
/// whitespace. The document <c>&lt;title&gt;</c> is pulled into metadata. Best-effort and
/// regex-based — not a full HTML parser (keeps the core zero-dependency).
/// </summary>
public sealed class HtmlExtractor : IFileExtractor
{
    // Script/style bodies are markup noise, not readable text — drop tag *and* content.
    private static readonly Regex ScriptOrStyle = new(
        @"<(script|style)\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex Comment = new(@"<!--.*?-->", RegexOptions.Singleline);
    private static readonly Regex Tag = new(@"<[^>]+>", RegexOptions.Singleline);
    private static readonly Regex Whitespace = new(@"\s+");
    private static readonly Regex TitleTag = new(
        @"<title\b[^>]*>(.*?)</title\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public bool CanHandle(string extension) => extension is ".html" or ".htm";

    public ExtractedContent Extract(string path)
    {
        var html = File.ReadAllText(path);

        var meta = new Dictionary<string, string>();
        var title = TitleTag.Match(html);
        if (title.Success)
        {
            var t = Clean(Tag.Replace(title.Groups[1].Value, " "));
            if (!string.IsNullOrWhiteSpace(t)) meta["Title"] = t;
        }

        var text = ScriptOrStyle.Replace(html, " ");
        text = Comment.Replace(text, " ");
        text = TitleTag.Replace(text, " ");   // already captured in metadata; not body text
        text = Tag.Replace(text, " ");
        return new ExtractedContent(meta, Clean(text));
    }

    // Decode entities, then collapse whitespace (tags become spaces, so runs are common).
    private static string Clean(string s) =>
        Whitespace.Replace(WebUtility.HtmlDecode(s), " ").Trim();
}
