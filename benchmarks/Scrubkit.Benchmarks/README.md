# Scrubkit.Benchmarks

A [BenchmarkDotNet](https://benchmarkdotnet.org/) throughput harness for `FolderScrubber`.
It generates a corpus of text-family files and measures how fast Scrubkit walks the folder
and extracts text + metadata.

This project is **deliberately kept out of `Scrubkit.slnx`** so it doesn't slow the main
build/CI. Run it on demand:

```sh
dotnet run -c Release --project benchmarks/Scrubkit.Benchmarks
```

## What it measures

- **Corpus:** 500 text-family files (`.txt`, `.md`, `.csv`, `.json`, `.log`, `.html`),
  ~2–8 KB each, generated deterministically in `[GlobalSetup]`.
- **Operation:** one full `FolderScrubber.ReadAsync` over the folder — enumerate → extract →
  normalize → clip → emit one `FileRecord` per file. No redaction (opt-in, left off).
- **`OperationsPerInvoke = 500`**, so the reported **Mean is time per file** — invert it for
  files/sec. It runs at `MaxDegreeOfParallelism` of 1 (sequential) and 4 (bounded parallel).

## Representative result

A recent run (net8.0 host, Windows 11; in-process toolchain). Your numbers will vary with
hardware, disk, and file mix — regenerate them with the command above.

| Parallelism | Mean / file | **Throughput**      |
| ----------- | ----------- | ------------------- |
| 1           | ~205 µs     | **~4,900 files/sec** |
| 4           | ~81 µs      | **~12,400 files/sec** |

So on a single machine Scrubkit extracts on the order of **thousands of files per second**,
scaling roughly 2.5× from sequential to 4-way parallel — comfortably fast for RAG-ingestion
and indexing pipelines.

> Extraction is I/O- and allocation-light: ~71 KB allocated per file across the pipeline.

## Notes

- The default BenchmarkDotNet toolchain generates and restores a separate project, which
  clashes with this repo's Central Package Management + lock files; `Program.cs` uses the
  **in-process toolchain** to avoid that. It still does full warmup/measurement.
- Throughput is dominated by the built-in text extractor here. PDF/Office/image corpora
  will read slower per file (heavier parsing); add fixtures and more `[Benchmark]` methods to
  measure them.
