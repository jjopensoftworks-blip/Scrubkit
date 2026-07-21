using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

/// <summary>Extension routing for every built-in extractor.</summary>
public class ExtractorCanHandleTests
{
    [Theory]
    [InlineData(".pdf", true)]
    [InlineData(".docx", false)]
    [InlineData(".txt", false)]
    [InlineData(".PDF", false)]   // caller passes a normalized lower-case extension
    public void Pdf(string ext, bool expected) =>
        Assert.Equal(expected, new PdfExtractor().CanHandle(ext));

    [Theory]
    [InlineData(".docx", true)]
    [InlineData(".pptx", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".doc", false)]
    [InlineData(".xls", false)]
    [InlineData(".odt", false)]
    [InlineData(".pdf", false)]
    public void Office(string ext, bool expected) =>
        Assert.Equal(expected, new OfficeExtractor().CanHandle(ext));

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".md", true)]
    [InlineData(".csv", true)]
    [InlineData(".log", true)]
    [InlineData(".json", true)]
    [InlineData(".xml", true)]
    [InlineData(".html", true)]
    [InlineData(".htm", true)]
    [InlineData(".rtf", true)]
    [InlineData(".pdf", false)]
    [InlineData(".docx", false)]
    [InlineData(".jpg", false)]
    [InlineData(".eml", false)]
    public void PlainText(string ext, bool expected) =>
        Assert.Equal(expected, new PlainTextExtractor().CanHandle(ext));

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".png", true)]
    [InlineData(".tiff", true)]
    [InlineData(".tif", true)]
    [InlineData(".heic", true)]
    [InlineData(".webp", true)]
    [InlineData(".gif", true)]
    [InlineData(".bmp", true)]
    [InlineData(".pdf", false)]
    [InlineData(".txt", false)]
    public void Image(string ext, bool expected) =>
        Assert.Equal(expected, new ImageExtractor().CanHandle(ext));
}
