// Copyright © 2026 jjopensoftworks-blip

using System;

namespace Scrubkit;

/// <summary>
/// Extension methods for configuring OpenDocument format extraction on <see cref="ReadOptions"/>.
/// </summary>
public static class OpenDocumentReadOptionsExtensions
{
    /// <summary>
    /// Registers OpenDocument format extractors (<c>.odt</c>, <c>.ods</c>, <c>.odp</c>) to <see cref="ReadOptions.Extractors"/>.
    /// </summary>
    /// <param name="options">The <see cref="ReadOptions"/> instance.</param>
    /// <returns>The <see cref="ReadOptions"/> instance for fluent chaining.</returns>
    public static ReadOptions AddOpenDocument(this ReadOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.AddExtractor<OpenDocumentExtractor>();
        return options;
    }
}
