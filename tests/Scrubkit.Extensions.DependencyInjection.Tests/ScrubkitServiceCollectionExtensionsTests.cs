using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scrubkit;
using Xunit;

namespace Scrubkit.Extensions.DependencyInjection.Tests;

public class ScrubkitServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "scrubkit-di-" + Guid.NewGuid().ToString("N"));

    public ScrubkitServiceCollectionExtensionsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    // A tiny extractor so we can prove configuration flows through to the resolved scrubber.
    private sealed class StubExtractor : IFileExtractor
    {
        public bool CanHandle(string extension) => extension == ".stub";
        public ExtractedContent Extract(string path) =>
            new(new Dictionary<string, string> { ["Kind"] = "stub" }, "stub-body");
    }

    [Fact]
    public void AddScrubkit_registers_resolvable_singleton()
    {
        var provider = new ServiceCollection().AddScrubkit().BuildServiceProvider();

        var a = provider.GetService<FolderScrubber>();
        var b = provider.GetService<FolderScrubber>();

        Assert.NotNull(a);
        Assert.Same(a, b);   // singleton
    }

    [Fact]
    public void AddScrubkit_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddScrubkit();
        services.AddScrubkit();

        Assert.Single(services, d => d.ServiceType == typeof(FolderScrubber));
    }

    [Fact]
    public async Task Configure_hook_flows_into_the_resolved_scrubber()
    {
        var provider = new ServiceCollection()
            .AddScrubkit(options => options.Extractors.Add(new StubExtractor()))
            .BuildServiceProvider();

        File.WriteAllText(Path.Combine(_dir, "file.stub"), "raw");

        var table = await provider.GetRequiredService<FolderScrubber>().ReadAsync(_dir);

        var row = Assert.Single(table);
        Assert.Equal("stub-body", row.Text);          // the stub extractor ran
        Assert.Equal("stub", row.Metadata["Kind"]);
    }

    [Fact]
    public void ServiceProvider_overload_receives_the_provider()
    {
        IServiceProvider? seen = null;

        var provider = new ServiceCollection()
            .AddScrubkit((sp, _) => seen = sp)
            .BuildServiceProvider();

        _ = provider.GetRequiredService<FolderScrubber>();   // triggers the factory

        Assert.NotNull(seen);
    }

    [Fact]
    public void AddScrubkit_null_services_throws()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddScrubkit());
    }

    [Fact]
    public async Task Bridges_per_file_diagnostics_to_ILogger()
    {
        var logs = new List<string>();
        var provider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(new CapturingLoggerFactory(logs))
            .AddScrubkit()
            .BuildServiceProvider();

        File.WriteAllText(Path.Combine(_dir, "a.txt"), "hi");
        await provider.GetRequiredService<FolderScrubber>().ReadAsync(_dir);

        Assert.Contains(logs, m => m.Contains("read", StringComparison.Ordinal));
    }

    private sealed class CapturingLoggerFactory(List<string> logs) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(logs);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    private sealed class CapturingLogger(List<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (logs) logs.Add(formatter(state, exception));
        }
    }
}
