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
IChatClient redactingClient = baseClient.UseRedaction(
    new StandardRedactor(new StandardRedactorOptions { Level = RedactionLevel.Standard })
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
}.AddAllExtractors(); // via Scrubkit.All (or .AddPdf().AddEmail() individually)

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

Ingest documents directly into Semantic Kernel's `ISemanticTextMemory` or modern `IVectorStoreRecordCollection<TKey, TRecord>` using `Scrubkit.Extensions.SemanticKernel`:

```csharp
using Microsoft.SemanticKernel.Memory;
using Scrubkit;

var scrubber = new FolderScrubber(
    new ReadOptions { Redaction = RedactionLevel.Standard }.AddAllExtractors());

// Option A: One-line import into ISemanticTextMemory (scans, redacts, chunks & saves)
IReadOnlyList<string> ids = await memory.ImportFolderAsync(
    collection: "knowledge-base",
    scrubber: scrubber,
    folderPath: @"C:\data\kb",
    chunkOptions: new ChunkOptions { MaxChars = 1000, OverlapChars = 100 });

// Option B: One-line import into IVectorStoreRecordCollection<string, MyVectorRecord>
IReadOnlyList<string> keys = await vectorCollection.ImportFolderAsync(
    scrubber: scrubber,
    folderPath: @"C:\data\kb",
    recordFactory: chunk => new MyVectorRecord
    {
        Id = $"{chunk.Path}#{chunk.Index}",
        Text = chunk.Text,
        SourceFile = chunk.Name
    });
```

---

## 5. Recipe 4: Exporting Sanitized Records to Parquet

For large-scale vector indexing or offline dataset creation, serialize sanitized records directly to Parquet using `Scrubkit.Parquet`:

```csharp
using Scrubkit;

var records = await new FolderScrubber(
    new ReadOptions { Redaction = RedactionLevel.Standard }.AddAllExtractors())
    .ReadAsync(@"C:\data\ingest");

await ParquetTableWriter.WriteFileAsync(records, @"C:\data\output\sanitized_ingestion.parquet");
```

---

## 6. Recipe 5: End-to-End AI Pipeline — `Folder → Scrubkit → LLM Summary → ShreePdf Report`

Every production .NET AI pipeline needs both **safe document ingestion** (`Scrubkit`) and **polished document output** (`ShreePdf`). Combine `Scrubkit`, `Microsoft.Extensions.AI`, and **[ShreePdf](https://www.nuget.org/packages/ShreePdf)** to ingest a directory of sensitive files, redact PII offline, generate an LLM executive summary, and render a vector PDF report with KPI cards and auto-fit pagination (zero Headless Chrome required):

```bash
dotnet add package Scrubkit.All
dotnet add package ShreePdf
```

```csharp
using Microsoft.Extensions.AI;
using Scrubkit;
using ShreePdf.Fluent;

// 1. Extract & Redact: Scan local directory (PDF, Word, Excel, Email) and scrub PII/secrets offline
var options = new ReadOptions
{
    Redaction = RedactionLevel.Standard,
    ComputeContentHash = true
}.AddAllExtractors();

var records = await new FolderScrubber(options).ReadAsync(@"C:\data\client-contracts");
int totalPiiRedacted = records.Sum(r => r.Redactions.Values.Sum());

// 2. Summarize with LLM: Build sanitized context and request structured Markdown output
string sanitizedContext = string.Join("\n\n", records.Select(r => $"## File: {r.Name}\n{r.Text}"));
ChatResponse aiResponse = await chatClient.GetResponseAsync(
    $"Produce an executive risk & compliance summary in Markdown for these {records.Count} documents:\n\n{sanitizedContext}"
);

// 3. Render Executive Vector PDF with ShreePdf (5ms native vector engine, no Chromium)
Document.FromMarkdown(aiResponse.Text, ThemePresets.SaaSAnalytics)
    .AddStatCards(
        new StatCard("Documents Scanned", records.Count.ToString(), "100% Offline", deltaPositive: true),
        new StatCard("PII Spans Redacted", totalPiiRedacted.ToString(), "Zero Leaks", deltaPositive: true)
    )
    .EnableAutoFit() // Squeezes 1-line orphan spills automatically
    .AddPageNumber("Page {page} of {pages}")
    .Save(@"C:\data\output\Executive-Compliance-Report.pdf");
```

> 🔗 **Explore ShreePdf:** [NuGet Package](https://www.nuget.org/packages/ShreePdf) · [Interactive Web Playground](https://jjopensoftworks-blip.github.io/ShreePdfPage/) · [GitHub Repository](https://github.com/jjopensoftworks-blip/ShreePdf)

---

## 7. Runnable Sample Project

Check out the runnable demo in `samples/Scrubkit.Sample.RagIngestion`:

```powershell
dotnet run -c Release --framework net10.0 --project samples/Scrubkit.Sample.RagIngestion
```
