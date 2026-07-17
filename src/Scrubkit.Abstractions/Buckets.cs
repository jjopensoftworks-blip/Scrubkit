namespace Scrubkit;

/// <summary>Coarse type buckets, shared by the reader and (optionally) extractors.</summary>
public static class Buckets
{
    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".heic", ".webp", ".gif", ".bmp" };

    public static bool IsImage(string ext) => ImageExts.Contains(ext);

    public static string For(string ext) => ext switch
    {
        ".pdf" or ".docx" or ".rtf" or ".odt"                     => "Document",
        ".xlsx" or ".xls" or ".csv" or ".ods"                     => "Spreadsheet",
        ".pptx" or ".ppt" or ".odp"                               => "Presentation",
        ".txt" or ".md" or ".log" or ".json" or ".xml" or ".html" or ".htm" => "Text",
        ".eml" or ".msg"                                          => "Email",
        _ when ImageExts.Contains(ext)                            => "Image",
        _                                                         => "Other",
    };
}
