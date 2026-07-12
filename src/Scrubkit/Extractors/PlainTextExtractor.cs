namespace Scrubkit;

/// <summary>Reads plain-text-ish files directly (.txt/.md/.csv/.log/.json/.xml/.html/.rtf).</summary>
public sealed class PlainTextExtractor : IFileExtractor
{
    private static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".md", ".csv", ".log", ".json", ".xml", ".html", ".htm", ".rtf" };

    public bool CanHandle(string extension) => Exts.Contains(extension);

    public ExtractedContent Extract(string path) =>
        new(new Dictionary<string, string>(), File.ReadAllText(path));
}
