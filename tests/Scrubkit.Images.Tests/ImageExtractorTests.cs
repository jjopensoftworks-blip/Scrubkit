// Copyright © 2026 jjopensoftworks-blip

using System;
using System.IO;
using Scrubkit;
using Xunit;

namespace Scrubkit.Images.Tests;

public class ImageExtractorTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".png", true)]
    [InlineData(".tiff", true)]
    public void ImageExtractor_CanHandleExtension_ReturnsExpected(string extension, bool expected)
    {
        var extractor = new ImageExtractor();
        Assert.Equal(expected, extractor.CanHandle(extension));
    }

    [Fact]
    public void ReadOptions_AddImages_RegistersImageExtractor()
    {
        var options = new ReadOptions().AddImages();
        Assert.Contains(options.Extractors, e => e is ImageExtractor);
    }

    [Fact]
    public void Extract_reads_make_model_software_from_exif()
    {
        var content = new ImageExtractor().Extract(Fixture("exif-sample.jpg"));

        Assert.Equal("TestCam", content.Metadata["Make"]);
        Assert.Equal("SK-100", content.Metadata["Model"]);
        Assert.Equal("Scrubkit", content.Metadata["Software"]);
        Assert.Equal("", content.Text);
    }
}
