using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class IncrementalTests : IDisposable
{
    private readonly string _dir;

    public IncrementalTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "scrubkit-incr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // ---- Manifest round-trip ----

    [Fact]
    public void Manifest_round_trips_through_save_and_load()
    {
        var entries = new[]
        {
            new ManifestEntry { Path = "C:/a b/file one.txt", SizeBytes = 12, Modified = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), ContentHash = "abc" },
            new ManifestEntry { Path = "C:/x/two.md", SizeBytes = 0, Modified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), ContentHash = null },
        };
        var manifest = new Manifest(entries);

        var sw = new StringWriter();
        manifest.Save(sw);
        var reloaded = Manifest.Load(new StringReader(sw.ToString()));

        Assert.Equal(2, reloaded.Count);
        Assert.True(reloaded.TryGet("C:/a b/file one.txt", out var e1));   // space in path preserved
        Assert.Equal(12, e1.SizeBytes);
        Assert.Equal(entries[0].Modified, e1.Modified);
        Assert.Equal("abc", e1.ContentHash);

        Assert.True(reloaded.TryGet("C:/x/two.md", out var e2));
        Assert.Null(e2.ContentHash);
    }

    [Fact]
    public void Manifest_load_skips_blank_comment_and_malformed_lines()
    {
        var text =
            "# scrubkit-manifest v1\n" +
            "\n" +
            "not-a-number\t123\t\tC:/bad.txt\n" +   // bad size
            "10\t637000000000000000\t\tC:/good.txt\n";
        var m = Manifest.Load(new StringReader(text));
        Assert.Equal(1, m.Count);
        Assert.True(m.TryGet("C:/good.txt", out _));
    }

    // ---- Incremental scan ----

    [Fact]
    public async Task First_run_treats_everything_as_changed_and_builds_a_manifest()
    {
        Write("a.txt", "hello");
        Write("b.txt", "world");
        var scrubber = new FolderScrubber();

        var result = await scrubber.ReadChangesAsync(_dir, Manifest.Empty);

        Assert.Equal(2, result.Changed.Count);
        Assert.Equal(2, result.Manifest.Count);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public async Task Second_run_skips_unchanged_and_reports_added_modified_removed()
    {
        var a = Write("a.txt", "unchanged content");
        var b = Write("b.txt", "will change");
        var c = Write("c.txt", "will be removed");
        var scrubber = new FolderScrubber();

        var first = await scrubber.ReadChangesAsync(_dir, Manifest.Empty);
        Assert.Equal(3, first.Changed.Count);

        // Mutate: modify b (and bump its mtime), remove c, add d.
        File.WriteAllText(b, "changed now, longer content");
        File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddSeconds(5));
        File.Delete(c);
        Write("d.txt", "brand new");

        var unchangedFired = new List<string>();
        var scrubber2 = new FolderScrubber(new ReadOptions
        {
            OnDiagnostic = d => { if (d.Event == "unchanged") lock (unchangedFired) unchangedFired.Add(d.Path); },
        });

        var second = await scrubber2.ReadChangesAsync(_dir, first.Manifest);

        // Only b (modified) and d (added) are extracted; a is skipped.
        var changedNames = second.Changed.Select(r => r.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "b.txt", "d.txt" }, changedNames);

        // a.txt was skipped as unchanged.
        Assert.Contains(a, unchangedFired);

        // c.txt is reported removed.
        Assert.Equal(new[] { c }, second.Removed.ToArray());

        // The new manifest is complete: a (carried), b, d — but not c.
        Assert.Equal(3, second.Manifest.Count);
        Assert.True(second.Manifest.TryGet(a, out _));
        Assert.False(second.Manifest.TryGet(c, out _));
    }

    [Fact]
    public async Task Unchanged_entry_is_carried_forward_verbatim_including_hash()
    {
        var a = Write("a.txt", "content with an email jane@example.com");
        var scrubber = new FolderScrubber(new ReadOptions { ComputeContentHash = true });

        var first = await scrubber.ReadChangesAsync(_dir, Manifest.Empty);
        Assert.True(first.Manifest.TryGet(a, out var firstEntry));
        Assert.NotNull(firstEntry.ContentHash);

        // No changes → second run carries the same entry (hash and all) with zero extractions.
        var second = await scrubber.ReadChangesAsync(_dir, first.Manifest);
        Assert.Empty(second.Changed);
        Assert.True(second.Manifest.TryGet(a, out var secondEntry));
        Assert.Equal(firstEntry, secondEntry);
    }

    [Fact]
    public async Task ExcludePaths_skips_listed_files()
    {
        var keep = Write("keep.txt", "hello");
        var skip = Write("skip.txt", "ignore me");

        var opts = new ReadOptions();
        opts.ExcludePaths.Add(skip);

        var table = await new FolderScrubber(opts).ReadAsync(_dir);

        Assert.Contains(table, r => r.Path == keep);
        Assert.DoesNotContain(table, r => r.Path == skip);
    }

    [Fact]
    public async Task Missing_root_throws()
    {
        var scrubber = new FolderScrubber();
        var missing = Path.Combine(_dir, "nope");
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => scrubber.ReadChangesAsync(missing, Manifest.Empty));
    }

    [Fact]
    public async Task Null_baseline_throws()
    {
        var scrubber = new FolderScrubber();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => scrubber.ReadChangesAsync(_dir, null!));
    }

    [Fact]
    public void Manifest_From_records_and_null_guards()
    {
        var rec = new FileRecord { Path = "p", Name = "p", Extension = ".txt", SizeBytes = 3, Modified = DateTime.UtcNow };
        var m = Manifest.From(new[] { rec });
        Assert.Equal(1, m.Count);
        Assert.Throws<ArgumentNullException>(() => Manifest.From(null!));
        Assert.Throws<ArgumentNullException>(() => new Manifest(null!));
        Assert.Throws<ArgumentNullException>(() => ManifestEntry.From(null!));
        Assert.Throws<ArgumentNullException>(() => Manifest.Empty.Save(null!));
        Assert.Throws<ArgumentNullException>(() => Manifest.Load(null!));
    }
}
