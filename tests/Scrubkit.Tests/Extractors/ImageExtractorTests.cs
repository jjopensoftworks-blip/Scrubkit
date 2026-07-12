using Scrubkit;
using Xunit;

namespace Scrubkit.Tests.Extractors;

public class ImageExtractorTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".tiff")]
    public void CanHandle_recognizes_image_extensions(string ext) =>
        Assert.True(new ImageExtractor().CanHandle(ext));

    [Fact]
    public void Extract_reads_make_model_software_from_exif()
    {
        var content = new ImageExtractor().Extract(Fixture("exif-sample.jpg"));

        Assert.Equal("TestCam", content.Metadata["Make"]);
        Assert.Equal("SK-100", content.Metadata["Model"]);
        Assert.Equal("Scrubkit", content.Metadata["Software"]);
        Assert.Equal("", content.Text);   // images carry no text
    }
}
