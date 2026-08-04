// Copyright © 2026 jjopensoftworks-blip

using System;

namespace Scrubkit;

/// <summary>
/// Extension methods for configuring legacy binary Office format extraction on <see cref="ReadOptions"/>.
/// </summary>
public static class LegacyOfficeReadOptionsExtensions
{
    /// <summary>
    /// Registers legacy binary Office format extractor (<c>.doc</c>, <c>.xls</c>, <c>.ppt</c>) to <see cref="ReadOptions.Extractors"/>.
    /// </summary>
    /// <param name="options">The <see cref="ReadOptions"/> instance.</param>
    /// <returns>The <see cref="ReadOptions"/> instance for fluent chaining.</returns>
    public static ReadOptions AddLegacyOffice(this ReadOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.AddExtractor<LegacyOfficeExtractor>();
        return options;
    }
}
