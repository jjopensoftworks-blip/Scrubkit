# Scrubkit.Abstractions

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Abstractions.svg)](https://www.nuget.org/packages/Scrubkit.Abstractions)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

The dependency-free **contracts** for
[Scrubkit](https://www.nuget.org/packages/Scrubkit) — interfaces and simple records,
nothing else.

Reference **this** package (instead of the full `Scrubkit` package) when you build an
add-on: a custom `IFileExtractor` for a new format, or a custom `IRedactor` for your own
redaction. Your add-on stays lightweight and takes no heavy dependencies.

---

## Example

```csharp
using Scrubkit;

public sealed class MyExtractor : IFileExtractor
{
    public bool CanHandle(string ext) => ext == ".xyz";
    public ExtractedContent Extract(string path) => new(metadata, text);
}
```

Everything lives in the flat `Scrubkit` namespace, so consuming code is identical whether you
reference `Scrubkit` or `Scrubkit.Abstractions`.

---

## What's inside

`IFileExtractor` · `ExtractedContent` · `IRedactor` · `RedactionResult` · `RedactionSpan` ·
`RedactionCategories` · `FileRecord` · `Chunk` · `ManifestEntry` · `ScrubDiagnostic` ·
`ReadOptions` · `Recursion` · `RedactionLevel` · `Buckets`

Multi-targets `net8.0`, `net10.0`, and `netstandard2.0`. Fully offline — zero external dependencies.
