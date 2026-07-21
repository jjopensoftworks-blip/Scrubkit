using System.IO.Compression;
using Scrubkit;
using Xunit;

namespace Scrubkit.Tests.Extractors;

public class OfficeExtractorTests : IDisposable
{
    private readonly List<string> _temp = new();

    public void Dispose()
    {
        foreach (var p in _temp)
            try { File.Delete(p); } catch { /* best-effort */ }
    }

    /// <summary>Writes a minimal OOXML (zip) file with the given entries.</summary>
    private string Ooxml(string ext, params (string name, string xml)[] entries)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ext);
        _temp.Add(path);
        using var zip = ZipFile.Open(path, ZipArchive_CreateMode);
        foreach (var (name, xml) in entries)
        {
            var entry = zip.CreateEntry(name);
            using var w = new StreamWriter(entry.Open());
            w.Write(xml);
        }
        return path;
    }

    private const ZipArchiveMode ZipArchive_CreateMode = ZipArchiveMode.Create;

    private const string Core =
        "<cp:coreProperties xmlns:cp=\"x\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">" +
        "<dc:title>Q3 Report</dc:title><dc:creator>author@example.com</dc:creator>" +
        "<dc:subject>Finance</dc:subject></cp:coreProperties>";

    [Fact]
    public void Docx_extracts_body_text_and_core_properties()
    {
        const string doc =
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
            "<w:body><w:p><w:r><w:t>Hello</w:t></w:r><w:r><w:t>world</w:t></w:r></w:p></w:body></w:document>";
        var path = Ooxml(".docx", ("word/document.xml", doc), ("docProps/core.xml", Core));

        var content = new OfficeExtractor().Extract(path);

        Assert.Contains("Hello", content.Text);
        Assert.Contains("world", content.Text);
        Assert.Equal("Q3 Report", content.Metadata["Title"]);
        Assert.Equal("author@example.com", content.Metadata["Author"]);
        Assert.Equal("Finance", content.Metadata["Subject"]);
    }

    [Fact]
    public void Xlsx_extracts_shared_strings()
    {
        const string shared =
            "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<si><t>Alpha</t></si><si><t>Beta</t></si></sst>";
        var path = Ooxml(".xlsx", ("xl/sharedStrings.xml", shared));

        var content = new OfficeExtractor().Extract(path);

        Assert.Contains("Alpha", content.Text);
        Assert.Contains("Beta", content.Text);
    }

    [Fact]
    public void Pptx_extracts_text_from_all_slides_in_order()
    {
        string Slide(string t) =>
            "<sld xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">" +
            $"<a:t>{t}</a:t></sld>";
        var path = Ooxml(".pptx",
            ("ppt/slides/slide1.xml", Slide("First")),
            ("ppt/slides/slide2.xml", Slide("Second")));

        var content = new OfficeExtractor().Extract(path);

        Assert.Contains("First", content.Text);
        Assert.Contains("Second", content.Text);
        Assert.True(content.Text.IndexOf("First", StringComparison.Ordinal)
                  < content.Text.IndexOf("Second", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_returns_core_props_but_no_body_for_an_unknown_office_extension()
    {
        // A valid OOXML container whose extension isn't one of the three known bodies:
        // core properties still read, but the body switch falls through to empty text.
        var path = Ooxml(".zipx", ("docProps/core.xml", Core));

        var content = new OfficeExtractor().Extract(path);

        Assert.Equal("", content.Text);
        Assert.Equal("Q3 Report", content.Metadata["Title"]);
    }

    [Fact]
    public async Task FolderScrubber_scrubs_pii_in_office_metadata()
    {
        // The author field is an email — it must be scrubbed out of the metadata too.
        var dir = Path.Combine(Path.GetTempPath(), "scrubkit-office-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var doc = "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                      "<w:body><w:p><w:r><w:t>body</w:t></w:r></w:p></w:body></w:document>";
            var path = Path.Combine(dir, "report.docx");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var (name, xml) in new[] { ("word/document.xml", doc), ("docProps/core.xml", Core) })
                {
                    using var w = new StreamWriter(zip.CreateEntry(name).Open());
                    w.Write(xml);
                }
            }

            var opts = new ReadOptions { Redaction = RedactionLevel.Standard };
            var rec = Assert.Single(await new FolderScrubber(opts).ReadAsync(dir));

            Assert.Equal("Document", rec.TypeBucket);
            Assert.Equal("[EMAIL]", rec.Metadata["Author"]);
            Assert.True(rec.Redactions["Email"] >= 1);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
