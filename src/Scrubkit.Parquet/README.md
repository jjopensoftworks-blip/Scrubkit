# Scrubkit.Parquet

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Parquet.svg)](https://www.nuget.org/packages/Scrubkit.Parquet)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

Writes a [**Scrubkit**](https://www.nuget.org/packages/Scrubkit) `FileRecord` table to
**Apache Parquet** (columnar) for data-lake and analytics ingestion, using
[Parquet.Net](https://www.nuget.org/packages/Parquet.Net).

The core `Scrubkit` package already ships **zero-dependency CSV and JSON** writers
(`TableWriter`). Reach for this package only when you specifically need Parquet — it keeps
that dependency out of the core.

## Install

```sh
dotnet add package Scrubkit.Parquet
```

## Use it

```csharp
using Scrubkit;

var table = await new FolderScrubber().ReadAsync(@"C:\Docs");

await ParquetTableWriter.WriteFileAsync(table, @"C:\out\files.parquet");
// or write to any stream:
await ParquetTableWriter.WriteAsync(table, stream);
```

Columns: `Path`, `Name`, `Extension`, `Folder`, `SizeBytes`, `Modified`, `TypeBucket`,
`Text`, `Warnings`, `Redactions`, `ContentHash`.

`Modified` is always stored in **UTC** — a Parquet timestamp is an instant, so read it back
and convert with `value.ToLocalTime()` if you need a local view. (The core's CSV/JSON
`TableWriter` additionally supports a `utc: false` option, since text formats can carry an
explicit offset; Parquet cannot.)

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
