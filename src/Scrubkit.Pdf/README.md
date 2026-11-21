# Scrubkit.Pdf

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Pdf.svg)](https://www.nuget.org/packages/Scrubkit.Pdf)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

PDF document text and metadata extractor (`Title`, `Author`, `Subject`, `PageCount`) for [**Scrubkit**](https://www.nuget.org/packages/Scrubkit).

Part of the [Scrubkit](https://github.com/jjopensoftworks-blip/Scrubkit) family — offline text & metadata extraction for .NET RAG and AI pipelines. Multi-targets `net8.0`, `net10.0`, and `netstandard2.0`.

## Install

```sh
dotnet add package Scrubkit.Pdf
```

## Usage

```csharp
using Scrubkit;

var options = new ReadOptions()
    .AddPdf(); // Registers PDF extractor (.pdf)

var scrubber = new FolderScrubber(options);
IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Docs");
```

Or bundle with all format extractors using [`Scrubkit.All`](https://www.nuget.org/packages/Scrubkit.All) via `.AddAllExtractors()`.

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
