namespace Scrubkit;

/// <summary>Camera/software metadata from images (no pixels, no OCR — that's an add-on).</summary>
public sealed class ImageExtractor : IFileExtractor
{
    public bool CanHandle(string extension) => Buckets.IsImage(extension);

    public ExtractedContent Extract(string path)
    {
        var meta = new Dictionary<string, string>();
        foreach (var d in MetadataExtractor.ImageMetadataReader.ReadMetadata(path))
            foreach (var tag in d.Tags)
                if (tag.Name is "Make" or "Model" or "Software" && !string.IsNullOrWhiteSpace(tag.Description))
                    meta[tag.Name] = tag.Description!.Trim();
        return new ExtractedContent(meta, "");
    }
}
