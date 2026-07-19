using System.Text;
using BenchmarkDotNet.Attributes;

namespace Scrubkit;

/// <summary>
/// Measures <see cref="FolderScrubber"/> throughput over a generated corpus of text-family
/// files. <c>OperationsPerInvoke</c> is set to the file count, so the reported "Mean" is
/// time <em>per file</em> — invert it for files/sec.
/// </summary>
[MemoryDiagnoser]
public class FolderScrubberBenchmarks
{
    private const int FileCount = 500;
    private static readonly string[] Extensions = { ".txt", ".md", ".csv", ".json", ".log", ".html" };

    private string _dir = "";

    /// <summary>Files processed concurrently — 1 (sequential) vs 4 (bounded parallel).</summary>
    [Params(1, 4)]
    public int Parallelism { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "scrubkit-bench");
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        Directory.CreateDirectory(_dir);
        GenerateCorpus(_dir, FileCount);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Benchmark(OperationsPerInvoke = FileCount)]
    public async Task<int> Read()
    {
        var scrubber = new FolderScrubber(new ReadOptions
        {
            MaxFiles = 0,                              // no cap — read the whole corpus
            MaxDegreeOfParallelism = Parallelism,
        });
        var table = await scrubber.ReadAsync(_dir);
        return table.Count;
    }

    // Deterministic corpus: a spread of text-family extensions and sizes (~2–8 KB each).
    private static void GenerateCorpus(string dir, int count)
    {
        const string paragraph =
            "The quick brown fox jumps over the lazy dog. Contact jane.doe@example.com " +
            "or call 555-0142 about invoice 4111-1111-1111-1111 dated 2026-07-01. ";

        for (var i = 0; i < count; i++)
        {
            var ext = Extensions[i % Extensions.Length];
            var lines = 20 + (i % 60);                 // varied length, no randomness
            var sb = new StringBuilder(paragraph.Length * lines);
            for (var l = 0; l < lines; l++) sb.AppendLine(paragraph);
            File.WriteAllText(Path.Combine(dir, $"file{i:D4}{ext}"), sb.ToString());
        }
    }
}
