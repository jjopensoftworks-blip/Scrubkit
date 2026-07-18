using System.IO.Compression;
using Scrubkit;
using Xunit;

namespace Scrubkit.OpenDocument.Tests;

public class OpenDocumentExtractorTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "scrubkit-odf-" + Guid.NewGuid().ToString("N"));

    public OpenDocumentExtractorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    // Builds a minimal ODF file (a zip with content.xml and optional meta.xml) and returns its path.
    private string Odf(string name, string contentXml, string? metaXml = null)
    {
        var path = Path.Combine(_dir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(zip, "content.xml", contentXml);
        if (metaXml != null) WriteEntry(zip, "meta.xml", metaXml);
        return path;
    }

    private static void WriteEntry(ZipArchive zip, string entryName, string content)
    {
        using var s = new StreamWriter(zip.CreateEntry(entryName).Open());
        s.Write(content);
    }

    private const string OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private const string TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private const string TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private const string DrawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private const string DcNs = "http://purl.org/dc/elements/1.1/";

    [Theory]
    [InlineData(".odt", true)]
    [InlineData(".ods", true)]
    [InlineData(".odp", true)]
    [InlineData(".docx", false)]
    [InlineData(".txt", false)]
    public void CanHandle_matches_odf_extensions(string ext, bool expected) =>
        Assert.Equal(expected, new OpenDocumentExtractor().CanHandle(ext));

    [Fact]
    public void Text_document_yields_body_and_properties()
    {
        var content =
            $"<office:document-content xmlns:office=\"{OfficeNs}\" xmlns:text=\"{TextNs}\">" +
            "<office:body><office:text>" +
            "<text:h>The Heading</text:h>" +
            "<text:p>First paragraph with <text:span>a span</text:span>.</text:p>" +
            "<text:p>Second paragraph.</text:p>" +
            "</office:text></office:body></office:document-content>";
        var meta =
            $"<office:document-meta xmlns:office=\"{OfficeNs}\" xmlns:dc=\"{DcNs}\">" +
            "<office:meta>" +
            "<dc:title>Quarterly Report</dc:title>" +
            "<dc:creator>Jane Doe</dc:creator>" +
            "<dc:subject>Finance</dc:subject>" +
            "</office:meta></office:document-meta>";

        var c = new OpenDocumentExtractor().Extract(Odf("doc.odt", content, meta));

        Assert.Contains("The Heading", c.Text);
        Assert.Contains("First paragraph with a span.", c.Text);
        Assert.Contains("Second paragraph.", c.Text);
        Assert.Equal("Quarterly Report", c.Metadata["Title"]);
        Assert.Equal("Jane Doe", c.Metadata["Author"]);
        Assert.Equal("Finance", c.Metadata["Subject"]);
    }

    [Fact]
    public void Spreadsheet_yields_cell_text()
    {
        var content =
            $"<office:document-content xmlns:office=\"{OfficeNs}\" xmlns:text=\"{TextNs}\" xmlns:table=\"{TableNs}\">" +
            "<office:body><office:spreadsheet><table:table>" +
            "<table:table-row>" +
            "<table:table-cell><text:p>Revenue</text:p></table:table-cell>" +
            "<table:table-cell><text:p>1250</text:p></table:table-cell>" +
            "</table:table-row>" +
            "</table:table></office:spreadsheet></office:body></office:document-content>";

        var c = new OpenDocumentExtractor().Extract(Odf("sheet.ods", content));

        Assert.Contains("Revenue", c.Text);
        Assert.Contains("1250", c.Text);
    }

    [Fact]
    public void Presentation_yields_slide_text()
    {
        var content =
            $"<office:document-content xmlns:office=\"{OfficeNs}\" xmlns:text=\"{TextNs}\" xmlns:draw=\"{DrawNs}\">" +
            "<office:body><office:presentation><draw:page>" +
            "<draw:frame><draw:text-box><text:p>Slide title</text:p></draw:text-box></draw:frame>" +
            "</draw:page></office:presentation></office:body></office:document-content>";

        var c = new OpenDocumentExtractor().Extract(Odf("deck.odp", content));

        Assert.Contains("Slide title", c.Text);
    }

    [Fact]
    public void Nested_paragraph_text_is_not_duplicated()
    {
        // A text box (with its own text:p) anchored inside a body paragraph — the inner text
        // must appear once, folded into the outer paragraph, not twice.
        var content =
            $"<office:document-content xmlns:office=\"{OfficeNs}\" xmlns:text=\"{TextNs}\" xmlns:draw=\"{DrawNs}\">" +
            "<office:body><office:text>" +
            "<text:p>Outer <draw:frame><draw:text-box><text:p>inner</text:p></draw:text-box></draw:frame> end.</text:p>" +
            "</office:text></office:body></office:document-content>";

        var text = new OpenDocumentExtractor().Extract(Odf("nested.odt", content)).Text;

        Assert.Equal(1, CountOccurrences(text, "inner"));
    }

    [Fact]
    public void Missing_content_returns_empty_without_throwing()
    {
        // A zip with no content.xml (e.g. not really an ODF file) yields an empty row.
        var path = Path.Combine(_dir, "empty.odt");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            WriteEntry(zip, "junk.txt", "not odf");

        var c = new OpenDocumentExtractor().Extract(path);

        Assert.Equal("", c.Text);
        Assert.Empty(c.Metadata);
    }

    [Fact]
    public async Task Routes_through_FolderScrubber_as_a_document_row()
    {
        var content =
            $"<office:document-content xmlns:office=\"{OfficeNs}\" xmlns:text=\"{TextNs}\">" +
            "<office:body><office:text><text:p>Delivered through the scrubber.</text:p>" +
            "</office:text></office:body></office:document-content>";
        Odf("routed.odt", content);

        var options = new ReadOptions();
        options.Extractors.Add(new OpenDocumentExtractor());

        var table = await new FolderScrubber(options).ReadAsync(_dir);

        var row = Assert.Single(table);
        Assert.Equal("Document", row.TypeBucket);
        Assert.Contains("Delivered through the scrubber.", row.Text);
        Assert.Empty(row.Warnings);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal);
             i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
