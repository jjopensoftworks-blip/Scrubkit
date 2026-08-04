// Copyright © 2026 jjopensoftworks-blip

using System;

namespace Scrubkit;

/// <summary>
/// Extension methods for registering all Scrubkit format extractors on <see cref="ReadOptions"/>.
/// </summary>
public static class ScrubkitAllExtensions
{
    /// <summary>
    /// Registers all available Scrubkit format extractors (<c>Email</c>, <c>OpenDocument</c>, <c>EPUB</c>, <c>LegacyOffice</c>) to <see cref="ReadOptions.Extractors"/>.
    /// </summary>
    /// <param name="options">The <see cref="ReadOptions"/> instance.</param>
    /// <returns>The <see cref="ReadOptions"/> instance for fluent chaining.</returns>
    public static ReadOptions AddAllExtractors(this ReadOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.AddEmail();
        options.AddOpenDocument();
        options.AddEpub();
        options.AddLegacyOffice();
        return options;
    }
}
