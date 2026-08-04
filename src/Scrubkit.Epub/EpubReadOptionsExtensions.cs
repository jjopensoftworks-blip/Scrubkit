// Copyright © 2026 jjopensoftworks-blip

using System;

namespace Scrubkit;

/// <summary>
/// Extension methods for configuring EPUB format extraction on <see cref="ReadOptions"/>.
/// </summary>
public static class EpubReadOptionsExtensions
{
    /// <summary>
    /// Registers EPUB format extractor (<c>.epub</c>) to <see cref="ReadOptions.Extractors"/>.
    /// </summary>
    /// <param name="options">The <see cref="ReadOptions"/> instance.</param>
    /// <returns>The <see cref="ReadOptions"/> instance for fluent chaining.</returns>
    public static ReadOptions AddEpub(this ReadOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.AddExtractor<EpubExtractor>();
        return options;
    }
}
