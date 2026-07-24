using Scrubkit;
using Xunit;

namespace Scrubkit.Tests.Extractors;

public class HtmlExtractorTests
{
    private static string Extract(string html, out ExtractedContent content, string ext = ".html")
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ext);
        File.WriteAllText(path, html);
        try { content = new HtmlExtractor().Extract(path); return content.Text; }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Strips_tags_and_returns_clean_text()
    {
        var text = Extract("<p>Hello <b>world</b></p>", out _);
        Assert.Equal("Hello world", text);
    }

    [Fact]
    public void Drops_script_and_style_bodies()
    {
        var text = Extract(
            "<style>.a{color:red}</style><p>Keep</p><script>var x = 1;</script>", out _);
        Assert.Equal("Keep", text);
        Assert.DoesNotContain("color", text);
        Assert.DoesNotContain("var x", text);
    }

    [Fact]
    public void Drops_comments()
    {
        var text = Extract("<!-- secret note --><p>Shown</p>", out _);
        Assert.Equal("Shown", text);
        Assert.DoesNotContain("secret", text);
    }

    [Fact]
    public void Decodes_entities()
    {
        var text = Extract("<p>Fish &amp; Chips &lt;3 caf&eacute;&nbsp;5&#39;</p>", out _);
        Assert.Contains("Fish & Chips", text);
        Assert.Contains("<3", text);
        Assert.Contains("café", text);
    }

    [Fact]
    public void Pulls_title_into_metadata()
    {
        Extract("<html><head><title>My Page</title></head><body>Hi</body></html>",
            out var content);
        Assert.Equal("My Page", content.Metadata["Title"]);
        Assert.Equal("Hi", content.Text);
    }

    [Fact]
    public void No_title_means_no_metadata()
    {
        Extract("<p>plain</p>", out var content);
        Assert.Empty(content.Metadata);
    }
}
