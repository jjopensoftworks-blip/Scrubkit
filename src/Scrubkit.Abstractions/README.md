# Scrubkit.Abstractions

The dependency-free contract layer for [Scrubkit](https://www.nuget.org/packages/Scrubkit).

Reference **this** package (not the full `Scrubkit` package) when you're writing an
add-on — a custom `IFileExtractor` for a new format or a custom `IRedactor` for
stronger PII handling. It carries only interfaces and simple records, so your add-on
stays lightweight and doesn't drag in PdfPig or MetadataExtractor.

```csharp
using Scrubkit;

public sealed class MyExtractor : IFileExtractor
{
    public bool CanHandle(string extension) => extension == ".xyz";
    public ExtractedContent Extract(string path) => new(metadata, text);
}
```

Everything lives in the `Scrubkit` namespace, so consuming code is identical whether
you reference `Scrubkit` or `Scrubkit.Abstractions`.

Contracts included: `IFileExtractor`, `ExtractedContent`, `IRedactor`,
`RedactionResult`, `FileRecord`, `ReadOptions`, `Recursion`, `RedactionLevel`,
`Buckets`.

Multi-targets `netstandard2.0` and `net8.0`. 100% offline — no dependencies.
