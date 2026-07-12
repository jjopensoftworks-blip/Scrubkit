using System.Text;

namespace Scrubkit;

/// <summary>Text (all pages, bounded) + info-dictionary metadata from PDFs.</summary>
public sealed class PdfExtractor : IFileExtractor
{
    private const int MaxRawChars = 500_000; // hard ceiling; final clip happens in the reader

    public bool CanHandle(string extension) => extension == ".pdf";

    public ExtractedContent Extract(string path)
    {
        var meta = new Dictionary<string, string>();
        using var doc = UglyToad.PdfPig.PdfDocument.Open(path);

        Put(meta, "Title", doc.Information.Title);
        Put(meta, "Author", doc.Information.Author);
        Put(meta, "Subject", doc.Information.Subject);
        meta["Pages"] = doc.NumberOfPages.ToString();

        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            sb.Append(page.Text).Append('\n');
            if (sb.Length > MaxRawChars) break;
        }
        return new ExtractedContent(meta, sb.ToString());
    }

    private static void Put(Dictionary<string, string> meta, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) meta[key] = value!.Trim();
    }
}
