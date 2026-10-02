// Copyright © 2026 jjopensoftworks-blip

namespace Scrubkit;

/// <summary>Fluent extension methods for registering PDF extractor support.</summary>
public static class PdfReadOptionsExtensions
{
    /// <summary>Registers PDF document extractor (<c>.pdf</c>).</summary>
    public static ReadOptions AddPdf(this ReadOptions options)
    {
        if (options is null) throw new System.ArgumentNullException(nameof(options));
        return options.AddExtractor<PdfExtractor>();
    }
}
