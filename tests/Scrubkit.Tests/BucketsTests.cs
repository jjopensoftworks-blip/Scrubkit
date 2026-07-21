using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class BucketsTests
{
    [Theory]
    // Documents
    [InlineData(".pdf", "Document")]
    [InlineData(".docx", "Document")]
    [InlineData(".rtf", "Document")]
    [InlineData(".odt", "Document")]
    [InlineData(".epub", "Document")]
    // Spreadsheets
    [InlineData(".xlsx", "Spreadsheet")]
    [InlineData(".xls", "Spreadsheet")]
    [InlineData(".csv", "Spreadsheet")]
    [InlineData(".ods", "Spreadsheet")]
    // Presentations
    [InlineData(".pptx", "Presentation")]
    [InlineData(".ppt", "Presentation")]
    [InlineData(".odp", "Presentation")]
    // Text family
    [InlineData(".txt", "Text")]
    [InlineData(".md", "Text")]
    [InlineData(".log", "Text")]
    [InlineData(".json", "Text")]
    [InlineData(".xml", "Text")]
    [InlineData(".html", "Text")]
    [InlineData(".htm", "Text")]
    // Email
    [InlineData(".eml", "Email")]
    [InlineData(".msg", "Email")]
    // Images
    [InlineData(".jpg", "Image")]
    [InlineData(".jpeg", "Image")]
    [InlineData(".png", "Image")]
    [InlineData(".tiff", "Image")]
    [InlineData(".tif", "Image")]
    [InlineData(".heic", "Image")]
    [InlineData(".webp", "Image")]
    [InlineData(".gif", "Image")]
    [InlineData(".bmp", "Image")]
    // Unknown
    [InlineData(".xyz", "Other")]
    [InlineData(".exe", "Other")]
    [InlineData(".zip", "Other")]
    [InlineData("", "Other")]
    public void For_maps_extension_to_bucket(string ext, string expected) =>
        Assert.Equal(expected, Buckets.For(ext));

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
    [InlineData(".JPG", true)]     // case-insensitive
    [InlineData(".HEIC", true)]
    [InlineData(".pdf", false)]
    [InlineData(".txt", false)]
    [InlineData(".docx", false)]
    [InlineData(".eml", false)]
    public void IsImage_detects_image_extensions(string ext, bool expected) =>
        Assert.Equal(expected, Buckets.IsImage(ext));
}
