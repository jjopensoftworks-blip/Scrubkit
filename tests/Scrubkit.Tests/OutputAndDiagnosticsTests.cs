using System.Text.Json;
using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class OutputAndDiagnosticsTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "scrubkit-io-" + Guid.NewGuid().ToString("N"));

    public OutputAndDiagnosticsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Content_hash_is_computed_when_enabled()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "hello");

        var table = await new FolderScrubber(new ReadOptions { ComputeContentHash = true }).ReadAsync(_dir);

        var row = Assert.Single(table);
        // Known SHA-256 of "hello" (UTF-8).
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", row.ContentHash);
    }

    [Fact]
    public async Task Content_hash_is_null_by_default()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "hello");
        var table = await new FolderScrubber().ReadAsync(_dir);
        Assert.Null(Assert.Single(table).ContentHash);
    }

    [Fact]
    public async Task Diagnostics_emit_a_read_event_per_file()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "hello");
        var diagnostics = new List<ScrubDiagnostic>();
        var options = new ReadOptions { OnDiagnostic = d => { lock (diagnostics) diagnostics.Add(d); } };

        await new FolderScrubber(options).ReadAsync(_dir);

        var read = Assert.Single(diagnostics, d => d.Event == "read" && !d.IsWarning);
        Assert.Equal(Path.Combine(_dir, "a.txt"), read.Path);
        Assert.False(string.IsNullOrEmpty(read.Message));
    }

    [Fact]
    public async Task Content_hash_warns_when_the_file_cannot_be_opened()
    {
        var path = Path.Combine(_dir, "locked.bin");   // no extractor: only the hash path runs
        File.WriteAllText(path, "data");

        // Hold the file open with no sharing so the SHA-256 read fails.
        using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var table = await new FolderScrubber(new ReadOptions { ComputeContentHash = true }).ReadAsync(_dir);

        var row = Assert.Single(table);
        Assert.Contains("hash-failed", row.Warnings);
        Assert.Null(row.ContentHash);
    }

    [Fact]
    public async Task Diagnostics_emit_a_warning_for_an_oversized_file()
    {
        File.WriteAllText(Path.Combine(_dir, "big.txt"), new string('x', 100));
        var diagnostics = new List<ScrubDiagnostic>();
        var options = new ReadOptions
        {
            MaxBytesPerFile = 10,
            OnDiagnostic = d => { lock (diagnostics) diagnostics.Add(d); },
        };

        await new FolderScrubber(options).ReadAsync(_dir);

        Assert.Contains(diagnostics, d => d.Event == "skipped-content" && d.IsWarning);
        Assert.DoesNotContain(diagnostics, d => d.Event == "read");   // skipped != read
    }

    [Fact]
    public async Task Modified_is_utc_and_serializes_with_a_z_suffix()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "hi");

        var table = await new FolderScrubber().ReadAsync(_dir);

        Assert.Equal(DateTimeKind.Utc, Assert.Single(table).Modified.Kind);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", TableWriter.ToCsv(table));
        using var doc = JsonDocument.Parse(TableWriter.ToJson(table));
        Assert.EndsWith("Z", doc.RootElement[0].GetProperty("modified").GetString());
    }

    [Fact]
    public async Task Table_writers_round_trip_a_scrubbed_folder()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "hello world");
        var table = await new FolderScrubber().ReadAsync(_dir);

        Assert.Contains("a.txt", TableWriter.ToCsv(table));

        using var doc = JsonDocument.Parse(TableWriter.ToJson(table));
        Assert.Equal("a.txt", doc.RootElement[0].GetProperty("name").GetString());
    }
}
