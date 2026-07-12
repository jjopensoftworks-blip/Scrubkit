using Scrubkit;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Scrubkit.Tests.Extractors;

public class PdfExtractorTests
{
    private static string WritePdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(text, 12, new PdfPoint(25, 700), font);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    [Fact]
    public void CanHandle_only_pdf()
    {
        var ex = new PdfExtractor();
        Assert.True(ex.CanHandle(".pdf"));
        Assert.False(ex.CanHandle(".txt"));
    }

    [Fact]
    public void Extract_returns_page_text_and_page_count()
    {
        var path = WritePdf("Contact bob@example.com today");
        try
        {
            var content = new PdfExtractor().Extract(path);

            Assert.Contains("bob@example.com", content.Text);
            Assert.Equal("1", content.Metadata["Pages"]);
        }
        finally { File.Delete(path); }
    }
}
