using System.IO.Compression;
using System.Text;
using Scrubkit;
using Xunit;

namespace Scrubkit.Epub.Tests;

public class EpubExtractorTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "scrubkit-epub-" + Guid.NewGuid().ToString("N"));

    public EpubExtractorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private const string ContainerXml =
        "<?xml version=\"1.0\"?>" +
        "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">" +
        "<rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles>" +
        "</container>";

    // Builds a minimal EPUB: mimetype + container + an OPF with the given metadata/spine + chapters.
    private string Epub(string name, string title, string author,
                        IReadOnlyList<(string id, string file, string html)> chapters,
                        IReadOnlyList<string>? spineOrder = null)
    {
        var path = Path.Combine(_dir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        Write(zip, "mimetype", "application/epub+zip");
        Write(zip, "META-INF/container.xml", ContainerXml);

        var manifest = new StringBuilder();
        foreach (var (id, file, _) in chapters)
            manifest.Append($"<item id=\"{id}\" href=\"{file}\" media-type=\"application/xhtml+xml\"/>");

        var spine = new StringBuilder();
        foreach (var id in spineOrder ?? chapters.Select(c => c.id).ToList())
            spine.Append($"<itemref idref=\"{id}\"/>");

        var opf =
            "<?xml version=\"1.0\"?>" +
            "<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\">" +
            "<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">" +
            $"<dc:title>{title}</dc:title><dc:creator>{author}</dc:creator><dc:subject>Fiction</dc:subject>" +
            "</metadata>" +
            $"<manifest>{manifest}</manifest><spine>{spine}</spine></package>";
        Write(zip, "OEBPS/content.opf", opf);

        foreach (var (_, file, html) in chapters)
            Write(zip, "OEBPS/" + file, html);

        return path;
    }

    private static void Write(ZipArchive zip, string entry, string content)
    {
        using var s = new StreamWriter(zip.CreateEntry(entry).Open());
        s.Write(content);
    }

    private static string Xhtml(string body) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>t</title>" +
        "<style>.x{color:red}</style></head><body>" + body + "</body></html>";

    [Theory]
    [InlineData(".epub", true)]
    [InlineData(".zip", false)]
    [InlineData(".odt", false)]
    public void CanHandle_matches_only_epub(string ext, bool expected) =>
        Assert.Equal(expected, new EpubExtractor().CanHandle(ext));

    [Fact]
    public void Extracts_metadata_and_body_text()
    {
        var path = Epub("book.epub", "The Great Novel", "A. Writer", new[]
        {
            ("c1", "ch1.xhtml", Xhtml("<h1>Chapter One</h1><p>It was a dark and stormy night.</p>")),
            ("c2", "ch2.xhtml", Xhtml("<p>The end came quietly.</p>")),
        });

        var c = new EpubExtractor().Extract(path);

        Assert.Equal("The Great Novel", c.Metadata["Title"]);
        Assert.Equal("A. Writer", c.Metadata["Author"]);
        Assert.Equal("Fiction", c.Metadata["Subject"]);
        Assert.Contains("Chapter One", c.Text);
        Assert.Contains("It was a dark and stormy night.", c.Text);
        Assert.Contains("The end came quietly.", c.Text);
        Assert.DoesNotContain("<p>", c.Text);         // tags stripped
        Assert.DoesNotContain("color:red", c.Text);   // <style> dropped
    }

    [Fact]
    public void Follows_spine_reading_order_not_manifest_order()
    {
        // Manifest lists c1 then c2, but the spine says c2 first.
        var path = Epub("ordered.epub", "T", "A", new[]
        {
            ("c1", "ch1.xhtml", Xhtml("<p>SECOND</p>")),
            ("c2", "ch2.xhtml", Xhtml("<p>FIRST</p>")),
        }, spineOrder: new[] { "c2", "c1" });

        var text = new EpubExtractor().Extract(path).Text;

        Assert.True(text.IndexOf("FIRST", StringComparison.Ordinal)
                    < text.IndexOf("SECOND", StringComparison.Ordinal),
            $"expected FIRST before SECOND, got: {text}");
    }

    [Fact]
    public void Decodes_html_entities()
    {
        var path = Epub("entities.epub", "T", "A", new[]
        {
            ("c1", "ch1.xhtml", Xhtml("<p>Tom &amp; Jerry&nbsp;&#8212; caf&#233;</p>")),
        });

        var text = new EpubExtractor().Extract(path).Text;

        Assert.Contains("Tom & Jerry", text);
        Assert.Contains("café", text);
    }

    [Fact]
    public void Missing_container_returns_empty_without_throwing()
    {
        var path = Path.Combine(_dir, "broken.epub");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            Write(zip, "mimetype", "application/epub+zip");

        var c = new EpubExtractor().Extract(path);

        Assert.Equal("", c.Text);
        Assert.Empty(c.Metadata);
    }

    [Fact]
    public async Task Routes_through_FolderScrubber_as_a_document_row()
    {
        Epub("routed.epub", "Routed Book", "A", new[]
        {
            ("c1", "ch1.xhtml", Xhtml("<p>Delivered through the scrubber.</p>")),
        });

        var options = new ReadOptions();
        options.Extractors.Add(new EpubExtractor());

        var table = await new FolderScrubber(options).ReadAsync(_dir);

        var row = Assert.Single(table);
        Assert.Equal("Document", row.TypeBucket);
        Assert.Equal("Routed Book", row.Metadata["Title"]);
        Assert.Contains("Delivered through the scrubber.", row.Text);
        Assert.Empty(row.Warnings);
    }
}
