#if NETSTANDARD2_0
namespace Scrubkit;

/// <summary>
/// Small shims for BCL surface that exists on net8.0 but not netstandard2.0.
/// Compiler-feature polyfills (records, required, init, ranges) come from PolySharp;
/// these are runtime-method shims PolySharp does not provide.
/// </summary>
internal static class Polyfills
{
    /// <summary>netstandard2.0 lacks <c>CollectionExtensions.GetValueOrDefault</c>.</summary>
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
        dictionary.TryGetValue(key, out var value) ? value : default;
}
#endif
