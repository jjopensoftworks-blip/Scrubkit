# Scrubkit.Extensions.SemanticKernel

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Extensions.SemanticKernel.svg)](https://www.nuget.org/packages/Scrubkit.Extensions.SemanticKernel)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

`Microsoft.SemanticKernel` integration for [**Scrubkit**](https://www.nuget.org/packages/Scrubkit) — scan, redact, chunk, and index folder contents directly into Semantic Kernel vector memory systems.

It multi-targets `netstandard2.0` and `net8.0` and provides extension methods for both `ISemanticTextMemory` and modern `IVectorStoreRecordCollection<TKey, TRecord>` vector stores.

## Install

```sh
dotnet add package Scrubkit.Extensions.SemanticKernel
```

## Quick Start

### 1. Ingesting Folders into ISemanticTextMemory

Automatically scan local file trees, extract text, sanitize PII/secrets, chunk using overlapping windows, and save directly to `ISemanticTextMemory`:

```csharp
using Microsoft.SemanticKernel.Memory;
using Scrubkit;

ISemanticTextMemory memory = ...;
var scrubber = new FolderScrubber(new ReadOptions { Redaction = RedactionLevel.Standard });

// Scans, redacts, chunks, and saves to memory collection "docs"
IReadOnlyList<string> ids = await memory.ImportFolderAsync("docs", scrubber, @"C:\Docs");
```

### 2. Ingesting Folders into IVectorStoreRecordCollection

Ingest local documents directly into a vector store record collection with custom record mapping:

```csharp
using Microsoft.Extensions.VectorData;
using Scrubkit;

IVectorStoreRecordCollection<string, MyVectorRecord> collection = ...;
var scrubber = new FolderScrubber();

IReadOnlyList<string> keys = await collection.ImportFolderAsync(
    scrubber,
    @"C:\Docs",
    chunk => new MyVectorRecord
    {
        Id = $"{chunk.Path}#{chunk.Index}",
        Text = chunk.Text,
        SourceFile = chunk.Name,
        ChunkIndex = chunk.Index
    });
```

### 3. Upserting Pre-Chunked Streams

If you already have a stream of `Chunk` records produced by `Chunker`, you can upsert them directly:

```csharp
IAsyncEnumerable<Chunk> chunkStream = ...;

await memory.SaveChunksAsync("docs", chunkStream);
// Or for Vector Stores:
await collection.UpsertChunksAsync(chunkStream, chunk => new MyVectorRecord { ... });
```

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
