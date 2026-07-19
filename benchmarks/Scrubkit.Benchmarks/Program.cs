using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Scrubkit;

// Run in-process: the default toolchain generates + restores a separate project, which
// clashes with this repo's Central Package Management and lock files. In-process still does
// proper warmup/measurement, and is plenty for a throughput figure.
var config = DefaultConfig.Instance
    .AddJob(Job.Default
        .WithToolchain(InProcessEmitToolchain.Instance)
        .WithWarmupCount(5)
        .WithIterationCount(15));

BenchmarkRunner.Run<FolderScrubberBenchmarks>(config);
