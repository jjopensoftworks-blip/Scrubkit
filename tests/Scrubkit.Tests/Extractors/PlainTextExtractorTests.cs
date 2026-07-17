using Scrubkit;
using Xunit;

namespace Scrubkit.Tests.Extractors;

public class PlainTextExtractorTests
{
    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".csv")]
    [InlineData(".json")]
    [InlineData(".htm")]
    [InlineData(".html")]
    [InlineData(".log")]
    [InlineData(".xml")]
    [InlineData(".rtf")]
    public void CanHandle_recognizes_text_extensions(string ext) =>
        Assert.True(new PlainTextExtractor().CanHandle(ext));

    [Fact]
    public void CanHandle_rejects_binary_extensions() =>
        Assert.False(new PlainTextExtractor().CanHandle(".pdf"));

    [Fact]
    public void Rtf_is_read_as_raw_text_including_markup()
    {
        // RTF is not parsed — it's read verbatim, so control words come through.
        // This documents the current behavior (see the "Supported formats" note).
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".rtf");
        File.WriteAllText(path, @"{\rtf1\ansi hello}");
        try
        {
            var content = new PlainTextExtractor().Extract(path);
            Assert.Contains(@"\rtf1", content.Text);   // raw markup, not stripped
            Assert.Contains("hello", content.Text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Extract_returns_file_contents_verbatim()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "hello world");
        try
        {
            var content = new PlainTextExtractor().Extract(path);
            Assert.Equal("hello world", content.Text);
            Assert.Empty(content.Metadata);
        }
        finally { File.Delete(path); }
    }
}
