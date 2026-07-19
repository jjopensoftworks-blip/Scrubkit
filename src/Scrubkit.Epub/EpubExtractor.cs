using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Scrubkit;

/// <summary>
/// Reads <c>.epub</c> e-books. An EPUB is a zip: <c>META-INF/container.xml</c> points at the
/// OPF package file, whose <c>&lt;metadata&gt;</c> holds the book's properties and whose
/// <c>&lt;spine&gt;</c> lists the XHTML content documents in reading order.
///
/// This reads the title/author/subject into metadata and concatenates the spine's text
/// (tags stripped) into <see cref="ExtractedContent.Text"/> — best-effort and fully offline,
/// using only the BCL, so it needs no dependencies beyond <c>Scrubkit.Abstractions</c>.
/// Register it via <see cref="ReadOptions.Extractors"/>.
/// </summary>
public sealed class EpubExtractor : IFileExtractor
{
    private static readonly XNamespace Container = "urn:oasis:names:tc:opendocument:xmlns:container";
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    /// <inheritdoc/>
    public bool CanHandle(string extension) => extension == ".epub";

    /// <inheritdoc/>
    public ExtractedContent Extract(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        var opfPath = OpfPath(zip);
        if (opfPath is null) return ExtractedContent.Empty;

        var opfEntry = zip.GetEntry(opfPath);
        if (opfEntry is null) return ExtractedContent.Empty;

        XDocument opf;
        using (var s = opfEntry.Open()) opf = XDocument.Load(s);

        return new ExtractedContent(Meta(opf), Content(zip, opf, ZipDir(opfPath)));
    }

    // META-INF/container.xml -> the OPF package path (relative to the zip root).
    private static string? OpfPath(ZipArchive zip)
    {
        var entry = zip.GetEntry("META-INF/container.xml");
        if (entry is null) return null;
        using var s = entry.Open();
        var xml = XDocument.Load(s);
        return xml.Descendants(Container + "rootfile")
            .FirstOrDefault()?.Attribute("full-path")?.Value;
    }

    private static Dictionary<string, string> Meta(XDocument opf)
    {
        var meta = new Dictionary<string, string>();
        Put(meta, "Title", opf.Descendants(Dc + "title").FirstOrDefault()?.Value);
        Put(meta, "Author", opf.Descendants(Dc + "creator").FirstOrDefault()?.Value);
        Put(meta, "Subject", opf.Descendants(Dc + "subject").FirstOrDefault()?.Value);
        return meta;
    }

    // Walk the spine (reading order), resolve each itemref -> manifest href -> zip entry,
    // strip the XHTML to text, and join.
    private static string Content(ZipArchive zip, XDocument opf, string baseDir)
    {
        var manifest = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in opf.Descendants(Opf + "item"))
        {
            var id = (string?)item.Attribute("id");
            var href = (string?)item.Attribute("href");
            if (id != null && href != null) manifest[id] = href;
        }

        var sb = new StringBuilder();
        foreach (var itemref in opf.Descendants(Opf + "itemref"))
        {
            var idref = (string?)itemref.Attribute("idref");
            if (idref is null || !manifest.TryGetValue(idref, out var href)) continue;

            var entry = zip.GetEntry(CombineZipPath(baseDir, href));
            if (entry is null) continue;

            using var reader = new StreamReader(entry.Open());
            var text = HtmlToText(reader.ReadToEnd());
            if (text.Length > 0) sb.Append(text).Append('\n');
        }
        return sb.ToString().Trim();
    }

    // ---- XHTML -> text (best-effort, dependency-free) -----------------------

    private static readonly Regex ScriptStyle =
        new(@"<(script|style)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex BlockTag =
        new(@"</?(?:p|div|h[1-6]|li|tr|br|blockquote|section)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnyTag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex HSpace = new(@"[ \t\f\v]+", RegexOptions.Compiled);
    private static readonly Regex PaddedNewline = new(@" *\n *", RegexOptions.Compiled);
    private static readonly Regex ManyNewlines = new(@"\n{2,}", RegexOptions.Compiled);

    private static string HtmlToText(string html)
    {
        html = ScriptStyle.Replace(html, " ");
        html = BlockTag.Replace(html, "\n");   // block boundaries become line breaks
        html = AnyTag.Replace(html, " ");       // drop the rest of the markup
        html = WebUtility.HtmlDecode(html);     // &amp; &nbsp; &#233; ... -> characters

        html = HSpace.Replace(html, " ");
        html = PaddedNewline.Replace(html, "\n");
        html = ManyNewlines.Replace(html, "\n");
        return html.Trim();
    }

    // ---- zip path helpers ---------------------------------------------------

    // Directory portion of a zip entry path ("OEBPS/content.opf" -> "OEBPS", "book.opf" -> "").
    private static string ZipDir(string entryPath)
    {
        var slash = entryPath.LastIndexOf('/');
        return slash < 0 ? "" : entryPath.Substring(0, slash);
    }

    // Resolve an href (relative to the OPF's folder) to a zip entry path, decoding %-escapes,
    // dropping any #fragment, and collapsing ./ and ../ segments.
    private static string CombineZipPath(string baseDir, string href)
    {
        href = Uri.UnescapeDataString(href);
        var hash = href.IndexOf('#');
        if (hash >= 0) href = href.Substring(0, hash);

        var combined = baseDir.Length == 0 ? href : baseDir + "/" + href;
        var parts = new List<string>();
        foreach (var part in combined.Split('/'))
        {
            if (part.Length == 0 || part == ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else parts.Add(part);
        }
        return string.Join("/", parts);
    }

    private static void Put(Dictionary<string, string> meta, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) meta[key] = value!.Trim();
    }
}
