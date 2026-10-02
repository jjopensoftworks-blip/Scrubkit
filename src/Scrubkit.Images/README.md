# Scrubkit.Images

Image EXIF metadata extractor for Scrubkit powered by [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet).

Part of the [Scrubkit](https://github.com/jjopensoftworks-blip/Scrubkit) family — offline text & metadata extraction for .NET RAG and AI pipelines.

## Install

```sh
dotnet add package Scrubkit.Images
```

## Usage

```csharp
using Scrubkit;

var options = new ReadOptions()
    .AddImages(); // Registers Image EXIF extractor (.jpg, .png, .tiff, etc.)

var scrubber = new FolderScrubber(options);
IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Docs");
```

Or bundle with all format extractors using [`Scrubkit.All`](https://www.nuget.org/packages/Scrubkit.All) via `.AddAllExtractors()`.

## License

[Mozilla Public License 2.0](https://mozilla.org/MPL/2.0/)
