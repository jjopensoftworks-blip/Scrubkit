using System.IO.Compression;
using System.Xml.Linq;

namespace Scrubkit;

/// <summary>
/// Reads OpenDocument Format files — text documents (<c>.odt</c>), spreadsheets
/// (<c>.ods</c>), and presentations (<c>.odp</c>) produced by LibreOffice / OpenOffice.
///
/// An ODF file is a zip containing <c>content.xml</c> (the body) and <c>meta.xml</c>
/// (document properties). This reads both straight from the zip with the BCL — the same
/// approach the built-in Office (OOXML) extractor uses — so it needs no dependencies beyond
/// <c>Scrubkit.Abstractions</c>. Register it via <see cref="ReadOptions.Extractors"/>.
/// </summary>
public sealed class OpenDocumentExtractor : IFileExtractor
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    /// <inheritdoc/>
    public bool CanHandle(string extension) => extension is ".odt" or ".ods" or ".odp";

    /// <inheritdoc/>
    public ExtractedContent Extract(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return new ExtractedContent(Meta(zip), Content(zip));
    }

    // The body of every ODF type is a tree of text:p (paragraph) and text:h (heading)
    // elements — inside office:text for .odt, table cells for .ods, and draw frames for .odp.
    // Collect only the outermost ones: a paragraph's value already includes any text nested
    // in it (e.g. a text box anchored inside it), so this captures everything exactly once.
    private static string Content(ZipArchive zip)
    {
        var entry = zip.GetEntry("content.xml");
        if (entry is null) return "";

        using var stream = entry.Open();
        var xml = XDocument.Load(stream);
        var body = xml.Descendants(Office + "body").FirstOrDefault();
        if (body is null) return "";

        var blocks = body.Descendants()
            .Where(e => (e.Name == Text + "p" || e.Name == Text + "h")
                        && !e.Ancestors().Any(a => a.Name == Text + "p" || a.Name == Text + "h"))
            .Select(e => e.Value);

        return string.Join("\n", blocks);
    }

    // Document properties live in meta.xml as Dublin Core elements — mirrors the fields the
    // built-in Office extractor surfaces (Title / Author / Subject).
    private static Dictionary<string, string> Meta(ZipArchive zip)
    {
        var meta = new Dictionary<string, string>();
        var entry = zip.GetEntry("meta.xml");
        if (entry is null) return meta;

        using var stream = entry.Open();
        var xml = XDocument.Load(stream);
        Put(meta, "Title", xml.Descendants(Dc + "title").FirstOrDefault()?.Value);
        Put(meta, "Author", xml.Descendants(Dc + "creator").FirstOrDefault()?.Value);
        Put(meta, "Subject", xml.Descendants(Dc + "subject").FirstOrDefault()?.Value);
        return meta;
    }

    private static void Put(Dictionary<string, string> meta, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) meta[key] = value!.Trim();
    }
}
