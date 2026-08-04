// Copyright © 2026 jjopensoftworks-blip

using System;

namespace Scrubkit;

/// <summary>
/// Extension methods for configuring email format extraction on <see cref="ReadOptions"/>.
/// </summary>
public static class EmailReadOptionsExtensions
{
    /// <summary>
    /// Registers email format extractors (<c>.eml</c> and <c>.msg</c>) to <see cref="ReadOptions.Extractors"/>.
    /// </summary>
    /// <param name="options">The <see cref="ReadOptions"/> instance.</param>
    /// <returns>The <see cref="ReadOptions"/> instance for fluent chaining.</returns>
    public static ReadOptions AddEmail(this ReadOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.AddExtractor<EmailExtractor>();
        options.AddExtractor<MsgExtractor>();
        return options;
    }
}
