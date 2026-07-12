using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class FolderScrubberTests : IDisposable
{
    private readonly string _dir;

    public FolderScrubberTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "scrubkit-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Missing_root_throws()
    {
        var scrubber = new FolderScrubber();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => scrubber.ReadAsync(Path.Combine(_dir, "does-not-exist")));
    }

    [Fact]
    public async Task Reads_text_file_and_scrubs_pii()
    {
        Write("notes.txt", "Contact jane@example.com or 192.168.1.1 for access.");

        var table = await new FolderScrubber().ReadAsync(_dir);

        var rec = Assert.Single(table);
        Assert.Equal("Text", rec.TypeBucket);
        Assert.True(rec.HasSensitiveData);
        Assert.DoesNotContain("jane@example.com", rec.Text);
        Assert.Contains("[EMAIL]", rec.Text);
    }

    [Fact]
    public async Task Unknown_type_yields_metadata_only_row()
    {
        Write("archive.bin", "binary-ish content with an email a@b.com");

        var table = await new FolderScrubber().ReadAsync(_dir);

        var rec = Assert.Single(table);
        Assert.Equal("Other", rec.TypeBucket);
        Assert.Equal("", rec.Text);            // no extractor => no content
        Assert.False(rec.HasSensitiveData);
    }

    [Fact]
    public async Task Recursion_top_only_ignores_nested_files()
    {
        Write("top.txt", "hello");
        Write(Path.Combine("nested", "deep.txt"), "world");

        var top = await new FolderScrubber(new ReadOptions { Recursion = Recursion.TopOnly }).ReadAsync(_dir);
        var all = await new FolderScrubber(new ReadOptions { Recursion = Recursion.AllNested }).ReadAsync(_dir);

        Assert.Single(top);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task MaxFiles_caps_the_batch()
    {
        for (int i = 0; i < 5; i++) Write($"f{i}.txt", "x");

        var table = await new FolderScrubber(new ReadOptions { MaxFiles = 3 }).ReadAsync(_dir);

        Assert.Equal(3, table.Count);
    }

    [Fact]
    public async Task Oversized_file_is_skipped_with_warning()
    {
        Write("big.txt", new string('a', 2048));

        var table = await new FolderScrubber(new ReadOptions { MaxBytesPerFile = 1024 }).ReadAsync(_dir);

        var rec = Assert.Single(table);
        Assert.Equal("", rec.Text);
        Assert.Contains(rec.Warnings, w => w.StartsWith("skipped-content"));
    }

    [Fact]
    public async Task Registered_extractor_overrides_builtins()
    {
        Write("notes.txt", "original text");
        var options = new ReadOptions();
        options.Extractors.Add(new StubExtractor());

        var table = await new FolderScrubber(options).ReadAsync(_dir);

        Assert.Equal("stubbed", Assert.Single(table).Text);
    }

    private sealed class StubExtractor : IFileExtractor
    {
        public bool CanHandle(string extension) => extension == ".txt";
        public ExtractedContent Extract(string path) =>
            new(new Dictionary<string, string>(), "stubbed");
    }
}
