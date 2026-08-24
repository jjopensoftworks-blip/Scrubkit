// Copyright © 2026 jjopensoftworks-blip

using System.Text.Json;
using Scrubkit;

namespace Scrubkit.Sample.RagIngestion;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine(" Scrubkit — RAG & AI Ingestion Sample Pipeline");
        Console.WriteLine("==================================================\n");

        // 1. Prepare a local test workspace directory with sample documents containing PII
        var tempFolder = Path.Combine(Path.GetTempPath(), "scrubkit-rag-demo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            SetupSampleFiles(tempFolder);

            // 2. Configure Scrubkit options: Offline ingestion, automatic PII redaction & chunking
            var options = new ReadOptions
            {
                Redaction = RedactionLevel.Standard,
                ComputeContentHash = true,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                OnDiagnostic = diag =>
                {
                    if (diag.IsWarning)
                        Console.WriteLine($"  [WARN] {Path.GetFileName(diag.Path)}: {diag.Message}");
                    else
                        Console.WriteLine($"  [INFO] {Path.GetFileName(diag.Path)}: {diag.Message}");
                }
            };

            Console.WriteLine($"[Step 1] Ingesting documents from: {tempFolder}");
            var scrubber = new FolderScrubber(options);
            var records = await scrubber.ReadAsync(tempFolder);
            Console.WriteLine($"\nSuccessfully processed {records.Count} document(s).\n");

            // 3. Display Redaction Results
            Console.WriteLine("[Step 2] Redaction Summary:");
            foreach (var r in records)
            {
                Console.WriteLine($"  📄 File: {r.Name} ({r.SizeBytes} bytes, Type: {r.TypeBucket})");
                Console.WriteLine($"     Hash: {r.ContentHash}");
                Console.WriteLine($"     Redactions: {string.Join(", ", r.Redactions.Select(kv => $"{kv.Key}={kv.Value}"))}");
                Console.WriteLine($"     Redacted Preview: \"{r.Text}\"\n");
            }

            // 4. Chunk Redacted Text for RAG / Vector Store Ingestion
            Console.WriteLine("[Step 3] Chunking Text for RAG Ingestion:");
            var chunker = new Chunker(new ChunkOptions { MaxChars = 150, OverlapChars = 20 });
            var allChunks = new List<Chunk>();

            foreach (var r in records)
            {
                var chunks = chunker.Chunk(r);
                allChunks.AddRange(chunks);
                Console.WriteLine($"  Document '{r.Name}' split into {chunks.Count} chunk(s):");
                foreach (var c in chunks)
                {
                    Console.WriteLine($"    • Chunk #{c.Index} [Offset {c.StartOffset}..{c.StartOffset + c.Text.Length}]: \"{c.Text}\"");
                }
            }

            // 5. Save Structured Table Output (JSON / CSV / Parquet)
            var jsonPath = Path.Combine(tempFolder, "rag_ingestion_records.json");
            File.WriteAllText(jsonPath, TableWriter.ToJson(records));
            Console.WriteLine($"\n[Step 4] Saved RAG ingestion manifest to: {jsonPath}");

            Console.WriteLine("\n[Summary] Pipeline complete! Scrubkit successfully scrubbed PII, normalized text without allocations, and generated RAG vector chunks.");
        }
        finally
        {
            try { Directory.Delete(tempFolder, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private static void SetupSampleFiles(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "customer_feedback.txt"),
            "Customer Alice Smith (email: alice.smith@example.com, phone: +1 (555) 019-2834) called regarding order #98765. Card ending in 4532 (4532-1234-5678-9010) was billed.");

        File.WriteAllText(Path.Combine(dir, "server_log.txt"),
            "2026-08-22 10:15:22 [INFO] Connection from IP 192.168.1.100. Auth token: ghp_1234567890abcdefghijklmnopqrstuvwxyz. Connection URI: postgresql://admin:secretPass123@db.internal:5432/prod");
    }
}
