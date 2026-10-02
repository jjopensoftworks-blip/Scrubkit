// Copyright © 2026 jjopensoftworks-blip

using System;
using System.IO;
using Scrubkit;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Scrubkit.Pdf.Tests;

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

    [Theory]
    [InlineData(".pdf", true)]
    [InlineData(".txt", false)]
    [InlineData(".docx", false)]
    public void PdfExtractor_CanHandleExtension_ReturnsExpected(string extension, bool expected)
    {
        var extractor = new PdfExtractor();
        Assert.Equal(expected, extractor.CanHandle(extension));
    }

    [Fact]
    public void ReadOptions_AddPdf_RegistersPdfExtractor()
    {
        var options = new ReadOptions().AddPdf();
        Assert.Contains(options.Extractors, e => e is PdfExtractor);
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
