using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class BucketsTests
{
    [Theory]
    [InlineData(".pdf", "Document")]
    [InlineData(".docx", "Document")]
    [InlineData(".xlsx", "Spreadsheet")]
    [InlineData(".csv", "Spreadsheet")]
    [InlineData(".pptx", "Presentation")]
    [InlineData(".txt", "Text")]
    [InlineData(".json", "Text")]
    [InlineData(".eml", "Email")]
    [InlineData(".jpg", "Image")]
    [InlineData(".png", "Image")]
    [InlineData(".xyz", "Other")]
    public void For_maps_extension_to_bucket(string ext, string expected) =>
        Assert.Equal(expected, Buckets.For(ext));

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".HEIC", true)]   // case-insensitive
    [InlineData(".pdf", false)]
    public void IsImage_detects_image_extensions(string ext, bool expected) =>
        Assert.Equal(expected, Buckets.IsImage(ext));
}
