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
    public void CanHandle_recognizes_text_extensions(string ext) =>
        Assert.True(new PlainTextExtractor().CanHandle(ext));

    [Fact]
    public void CanHandle_rejects_binary_extensions() =>
        Assert.False(new PlainTextExtractor().CanHandle(".pdf"));

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
