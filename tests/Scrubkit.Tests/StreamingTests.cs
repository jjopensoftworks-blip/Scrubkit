using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class StreamingTests : IDisposable
{
    private readonly string _dir;

    public StreamingTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "scrubkit-stream-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    private void Write(string name, string content) =>
        File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public async Task Stream_yields_same_records_as_batch()
    {
        Write("a.txt", "1");
        Write("b.txt", "2");

        var batch = await new FolderScrubber().ReadAsync(_dir);
        var streamed = await Collect(new FolderScrubber().ReadStreamAsync(_dir));

        Assert.Equal(
            batch.Select(r => r.Path).OrderBy(x => x),
            streamed.Select(r => r.Path).OrderBy(x => x));
    }

    [Fact]
    public async Task Stream_respects_MaxFiles()
    {
        for (int i = 0; i < 5; i++) Write($"f{i}.txt", "x");

        var streamed = await Collect(
            new FolderScrubber(new ReadOptions { MaxFiles = 3 }).ReadStreamAsync(_dir));

        Assert.Equal(3, streamed.Count);
    }

    [Fact]
    public async Task Stream_missing_root_throws()
    {
        var scrubber = new FolderScrubber();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
        {
            await foreach (var _ in scrubber.ReadStreamAsync(Path.Combine(_dir, "nope")))
            {
            }
        });
    }

    [Fact]
    public async Task Default_options_run_sequentially()
    {
        for (int i = 0; i < 6; i++) Write($"f{i}.txt", "x");
        var probe = new ConcurrencyProbe();
        var options = new ReadOptions();               // MaxDegreeOfParallelism defaults to 1
        options.Extractors.Add(probe);

        await new FolderScrubber(options).ReadAsync(_dir);

        Assert.Equal(1, probe.MaxObserved);
    }

    [Fact]
    public async Task Parallelism_is_bounded_by_MaxDegreeOfParallelism()
    {
        // Ensure the thread pool has workers ready, so the Task.Run work actually overlaps.
        // Without this, a constrained CI runner can inject pool threads slower than the short
        // per-file work completes and serialize it — making this concurrency check flaky.
        ThreadPool.GetMinThreads(out var minWorker, out var minIo);
        ThreadPool.SetMinThreads(Math.Max(minWorker, 8), minIo);

        for (int i = 0; i < 12; i++) Write($"f{i}.txt", "x");
        var probe = new ConcurrencyProbe();
        var options = new ReadOptions { MaxDegreeOfParallelism = 4 };
        options.Extractors.Add(probe);

        var table = await new FolderScrubber(options).ReadAsync(_dir);

        Assert.Equal(12, table.Count);
        Assert.True(probe.MaxObserved > 1, $"expected parallel execution, observed {probe.MaxObserved}");
        Assert.True(probe.MaxObserved <= 4, $"expected bound of 4, observed {probe.MaxObserved}");
    }

    [Fact]
    public async Task Order_is_preserved_under_parallelism()
    {
        for (int i = 0; i < 20; i++) Write($"f{i}.txt", i.ToString());

        var sequential = await new FolderScrubber(new ReadOptions()).ReadAsync(_dir);
        var parallel = await Collect(
            new FolderScrubber(new ReadOptions { MaxDegreeOfParallelism = 4 }).ReadStreamAsync(_dir));

        // Same underlying enumeration order; parallelism must not reorder the output.
        Assert.Equal(sequential.Select(r => r.Path), parallel.Select(r => r.Path));
    }

    private static async Task<List<FileRecord>> Collect(IAsyncEnumerable<FileRecord> source)
    {
        var list = new List<FileRecord>();
        await foreach (var r in source) list.Add(r);
        return list;
    }

    /// <summary>Extractor that records the peak number of concurrent Extract calls.</summary>
    private sealed class ConcurrencyProbe : IFileExtractor
    {
        private int _current;
        public int MaxObserved;

        public bool CanHandle(string extension) => extension == ".txt";

        public ExtractedContent Extract(string path)
        {
            int now = Interlocked.Increment(ref _current);
            int prev;
            while (now > (prev = Volatile.Read(ref MaxObserved)))
                Interlocked.CompareExchange(ref MaxObserved, now, prev);
            Thread.Sleep(50);   // hold the slot so overlaps are observable
            Interlocked.Decrement(ref _current);
            return new ExtractedContent(new Dictionary<string, string>(), "x");
        }
    }
}
