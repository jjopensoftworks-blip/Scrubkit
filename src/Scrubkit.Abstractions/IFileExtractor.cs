namespace Scrubkit;

/// <summary>Text + embedded metadata pulled from one file.</summary>
public readonly struct ExtractedContent
{
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public string Text { get; }

    public ExtractedContent(IReadOnlyDictionary<string, string> metadata, string text)
    {
        Metadata = metadata;
        Text = text;
    }

    public static ExtractedContent Empty { get; } =
        new(new Dictionary<string, string>(), "");
}

/// <summary>
/// The extension point. The core ships fast extractors for the common formats;
/// add-on packages (e.g. Scrubkit.Email) implement this for more types and are
/// registered via <see cref="ReadOptions.Extractors"/>. Registered extractors are
/// tried before the built-ins, so an add-on can also override a built-in.
///
/// <see cref="Extract"/> may throw — <c>FolderScrubber</c> isolates failures
/// per file and surfaces them as warnings, never crashing the batch.
/// </summary>
public interface IFileExtractor
{
    /// <summary>True if this extractor handles the given lower-case extension (with dot).</summary>
    bool CanHandle(string extension);

    /// <summary>Open the file and return its text + metadata. Best-effort.</summary>
    ExtractedContent Extract(string path);
}
