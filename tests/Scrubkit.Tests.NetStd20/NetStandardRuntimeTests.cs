using System.Reflection;
using System.Runtime.Versioning;
using Scrubkit;
using Xunit;

namespace Scrubkit.Tests.NetStd20;

/// <summary>
/// These run the <c>netstandard2.0</c> build of Scrubkit on the .NET 8 test host (the
/// project reference is pinned to that TFM). The point is to exercise the polyfilled code
/// paths — PolySharp records / <c>init</c> / ranges and the <c>GetValueOrDefault</c> shim —
/// at runtime, catching anything that compiles but misbehaves under the polyfills.
/// </summary>
public class NetStandardRuntimeTests : IDisposable
{
    private readonly string _dir;

    public NetStandardRuntimeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "scrubkit-ns20-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void The_loaded_scrubkit_assembly_is_the_netstandard2_0_build()
    {
        var tfm = typeof(FolderScrubber).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        Assert.Equal(".NETStandard,Version=v2.0", tfm);
    }

    [Fact]
    public async Task Clips_long_text_using_the_range_polyfill()
    {
        File.WriteAllText(Path.Combine(_dir, "long.txt"), new string('a', 300));

        // MaxTextLength drives `t[..MaxTextLength]`, a range slice polyfilled on ns2.0.
        var opts = new ReadOptions { MaxTextLength = 50 };   // object init → polyfilled setters
        var table = await new FolderScrubber(opts).ReadAsync(_dir);   // IAsyncEnumerable path

        var rec = Assert.Single(table);   // FileRecord is a polyfilled record type
        Assert.Equal(50, rec.Text.Length);
        Assert.Contains("text-clipped", rec.Warnings);
    }

    [Fact]
    public async Task Folds_metadata_redaction_counts_via_the_getvalueordefault_shim()
    {
        File.WriteAllText(Path.Combine(_dir, "record.dat"), "unused");

        var opts = new ReadOptions { Redaction = RedactionLevel.Standard };
        opts.Extractors.Add(new MetaExtractor());

        var rec = Assert.Single(await new FolderScrubber(opts).ReadAsync(_dir));

        // Text redaction + the metadata fold (which calls the ns2.0 GetValueOrDefault shim).
        Assert.Contains("[PHONE]", rec.Text);
        Assert.True(rec.Redactions.GetValueOrDefault("Email") >= 1);
        Assert.True(rec.Redactions.GetValueOrDefault("Phone") >= 1);
    }

    // Returns metadata that itself carries a redactable value, so redaction runs over both
    // text and metadata — the path that hits the GetValueOrDefault shim in FolderScrubber.
    private sealed class MetaExtractor : IFileExtractor
    {
        public bool CanHandle(string extension) => extension == ".dat";

        public ExtractedContent Extract(string path) => new(
            new Dictionary<string, string> { ["Author"] = "jane@example.com" },
            "call 555-123-4567 for details");
    }
}
