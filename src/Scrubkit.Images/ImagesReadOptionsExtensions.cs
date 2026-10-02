// Copyright © 2026 jjopensoftworks-blip

namespace Scrubkit;

/// <summary>Fluent extension methods for registering Image EXIF extractor support.</summary>
public static class ImagesReadOptionsExtensions
{
    /// <summary>Registers Image EXIF metadata extractor (<c>.jpg</c>, <c>.png</c>, <c>.tiff</c>, etc.).</summary>
    public static ReadOptions AddImages(this ReadOptions options)
    {
        if (options is null) throw new System.ArgumentNullException(nameof(options));
        return options.AddExtractor<ImageExtractor>();
    }
}
