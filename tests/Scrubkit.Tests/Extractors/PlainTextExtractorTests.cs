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
    [InlineData(".log")]
    [InlineData(".xml")]
    public void CanHandle_recognizes_text_extensions(string ext) =>
        Assert.True(new PlainTextExtractor().CanHandle(ext));

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".html")]   // HtmlExtractor's job now
    [InlineData(".rtf")]    // RtfExtractor's job now
    public void CanHandle_rejects_non_plain_extensions(string ext) =>
        Assert.False(new PlainTextExtractor().CanHandle(ext));

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
