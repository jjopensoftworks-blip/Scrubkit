# RAG & AI Ingestion Recipes — Scrubkit

Scrubkit is an offline, multi-format text and metadata extraction library designed to sanitize documents before passing them to LLMs, vector databases, and RAG (Retrieval-Augmented Generation) pipelines.

---

## 1. Core Principles for RAG Ingestion

1. **Zero Data Leaks**: Redact sensitive PII, API keys, credentials, and connection strings **before** generating vector embeddings.
2. **Local-First & Offline**: Process files on-premises or on-device without cloud or network API calls.
3. **Structured Chunking**: Snap chunk boundaries to whitespace to avoid clipping words mid-token.

---

## 2. Recipe 1: Scrubkit + `Microsoft.Extensions.AI` Middleware

Use `Scrubkit.Extensions.MicrosoftExtensionsAI` to wrap your `IChatClient` or `IEmbeddingGenerator` with offline PII redaction middleware:

```csharp
using Microsoft.Extensions.AI;
using Scrubkit;

// Create your base chat client or embedding generator
IChatClient baseClient = new YourLocalOllamaOrAzureChatClient();

// Wrap with Scrubkit redaction middleware
IChatClient redactingClient = baseClient.AsRedacting(
    level: RedactionLevel.Standard
);

// Any sensitive user prompt or document context is scrubbed BEFORE hitting the model
ChatResponse response = await redactingClient.GetResponseAsync(
    "Please analyze account details for user email: john.doe@acme.com, card: 4532-1234-5678-9010"
);
```

---

## 3. Recipe 2: Folder Scanning → Redaction → Vector Chunks

Extract text across a directory of mixed files (PDF, Word, Excel, Email, Plain Text), scrub PII, and generate chunks ready for vector indexing:

```csharp
using Scrubkit;

var options = new ReadOptions
{
    Redaction = RedactionLevel.Standard,
    ComputeContentHash = true,
    MaxDegreeOfParallelism = Environment.ProcessorCount
};

var scrubber = new FolderScrubber(options);
IReadOnlyList<FileRecord> records = await scrubber.ReadAsync(@"C:\data\documents");

// Initialize chunker (e.g. 500-character windows with 50-character overlap)
var chunker = new Chunker(new ChunkOptions
{
    MaxChars = 500,
    OverlapChars = 50,
    RespectWordBoundaries = true
});

foreach (var record in records)
{
    IReadOnlyList<Chunk> chunks = chunker.Chunk(record);
    foreach (var chunk in chunks)
    {
        // Pass chunk.Text and chunk.Metadata to your embedding generator & vector database
        Console.WriteLine($"[Chunk #{chunk.Index}] Source: {chunk.Name}");
        Console.WriteLine($"Text: {chunk.Text}");
    }
}
```

---

## 4. Recipe 3: Semantic Kernel Integration

Ingest documents directly into Semantic Kernel's `IVectorStoreRecordCollection` using `Scrubkit.Extensions.SemanticKernel`:

```csharp
using Microsoft.SemanticKernel.Memory;
using Scrubkit;

var scrubber = new FolderScrubber(new ReadOptions { Redaction = RedactionLevel.Standard });
var records = await scrubber.ReadAsync(@"C:\data\kb");

var chunker = new Chunker(new ChunkOptions { MaxChars = 1000, OverlapChars = 100 });

foreach (var record in records)
{
    var chunks = chunker.Chunk(record);
    foreach (var chunk in chunks)
    {
        // Store sanitized text chunk into Semantic Memory
        await memory.SaveInformationAsync(
            collection: "knowledge-base",
            text: chunk.Text,
            id: $"{chunk.Name}_{chunk.Index}",
            description: $"Ingested from {chunk.Path}"
        );
    }
}
```

---

## 5. Recipe 4: Exporting Sanitized Records to Parquet

For large-scale vector indexing or offline dataset creation, serialize sanitized records directly to Parquet using `Scrubkit.Parquet`:

```csharp
using Scrubkit;
using Scrubkit.Parquet;

var records = await new FolderScrubber(new ReadOptions { Redaction = RedactionLevel.Standard })
    .ReadAsync(@"C:\data\ingest");

await ParquetTableWriter.WriteAsync(records, @"C:\data\output\sanitized_ingestion.parquet");
```

---

## 6. Runnable Sample Project

Check out the runnable demo in `samples/Scrubkit.Sample.RagIngestion`:

```powershell
dotnet run -c Release --framework net10.0 --project samples/Scrubkit.Sample.RagIngestion
```
