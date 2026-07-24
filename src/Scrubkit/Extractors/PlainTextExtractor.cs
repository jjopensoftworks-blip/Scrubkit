namespace Scrubkit;

/// <summary>Reads plain-text-ish files directly (.txt/.md/.csv/.log/.json/.xml).</summary>
public sealed class PlainTextExtractor : IFileExtractor
{
    // HTML/RTF are handled by dedicated cleaning extractors, not read raw here.
    private static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".md", ".csv", ".log", ".json", ".xml" };

    public bool CanHandle(string extension) => Exts.Contains(extension);

    public ExtractedContent Extract(string path) =>
        new(new Dictionary<string, string>(), File.ReadAllText(path));
}
