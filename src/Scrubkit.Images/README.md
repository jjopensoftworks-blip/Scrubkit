# Scrubkit.Images

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Images.svg)](https://www.nuget.org/packages/Scrubkit.Images)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

Image EXIF metadata extractor (`.jpg`, `.jpeg`, `.png`, `.tiff`, `.tif`, `.heic`, `.webp`, `.gif`, `.bmp`) for [**Scrubkit**](https://www.nuget.org/packages/Scrubkit).

Part of the [Scrubkit](https://github.com/jjopensoftworks-blip/Scrubkit) family — offline text & metadata extraction for .NET RAG and AI pipelines. Multi-targets `net8.0`, `net10.0`, and `netstandard2.0`.

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
IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Photos");
```

Or bundle with all format extractors using [`Scrubkit.All`](https://www.nuget.org/packages/Scrubkit.All) via `.AddAllExtractors()`.

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
