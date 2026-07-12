using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Scrubkit;

/// <summary>Text + core properties from OOXML Office files (.docx / .pptx / .xlsx).</summary>
public sealed class OfficeExtractor : IFileExtractor
{
    private static readonly XNamespace W  = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace A  = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace S  = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public bool CanHandle(string extension) => extension is ".docx" or ".pptx" or ".xlsx";

    public ExtractedContent Extract(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var meta = CoreProps(zip);
        var text = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".docx" => Docx(zip),
            ".pptx" => Pptx(zip),
            ".xlsx" => Xlsx(zip),
            _ => "",
        };
        return new ExtractedContent(meta, text);
    }

    private static string Docx(ZipArchive zip)
    {
        var body = zip.GetEntry("word/document.xml");
        if (body is null) return "";
        using var s = body.Open();
        var xml = XDocument.Load(s);
        return string.Join(" ", xml.Descendants(W + "t").Select(e => e.Value));
    }

    private static string Pptx(ZipArchive zip)
    {
        var sb = new StringBuilder();
        foreach (var p in zip.Entries
            .Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                        && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName))
        {
            using var s = p.Open();
            var xml = XDocument.Load(s);
            foreach (var t in xml.Descendants(A + "t")) sb.Append(t.Value).Append(' ');
        }
        return sb.ToString();
    }

    private static string Xlsx(ZipArchive zip)
    {
        var shared = zip.GetEntry("xl/sharedStrings.xml");
        if (shared is null) return "";
        using var s = shared.Open();
        var xml = XDocument.Load(s);
        return string.Join(" ", xml.Descendants(S + "t").Select(e => e.Value));
    }

    private static Dictionary<string, string> CoreProps(ZipArchive zip)
    {
        var meta = new Dictionary<string, string>();
        var core = zip.GetEntry("docProps/core.xml");
        if (core is null) return meta;
        using var s = core.Open();
        var xml = XDocument.Load(s);
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
