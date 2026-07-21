using Scrubkit;
using Xunit;

namespace Scrubkit.Tool.Tests;

public class CliTests
{
    // ---- Options.Parse ----

    [Fact]
    public void Parses_folder_and_defaults()
    {
        var o = Options.Parse(new[] { "./docs" });
        Assert.Equal("./docs", o.Folder);
        Assert.True(o.Recurse);
        Assert.Equal(RedactionLevel.Off, o.Redaction);
        Assert.Equal(OutputFormat.Csv, o.Format);
        Assert.Null(o.Out);
        Assert.False(o.Hash);
        Assert.True(o.Utc);
    }

    [Theory]
    [InlineData("csv", OutputFormat.Csv)]
    [InlineData("json", OutputFormat.Json)]
    [InlineData("jsonl", OutputFormat.JsonLines)]
    [InlineData("ndjson", OutputFormat.JsonLines)]
    [InlineData("parquet", OutputFormat.Parquet)]
    public void Parses_format(string value, OutputFormat expected)
    {
        Assert.Equal(expected, Options.Parse(new[] { "f", "--format", value }).Format);
        Assert.Equal(expected, Options.Parse(new[] { "f", $"--format={value}" }).Format);
    }

    [Theory]
    [InlineData("out.csv", OutputFormat.Csv)]
    [InlineData("out.json", OutputFormat.Json)]
    [InlineData("out.jsonl", OutputFormat.JsonLines)]
    [InlineData("out.parquet", OutputFormat.Parquet)]
    public void Infers_format_from_out_extension(string file, OutputFormat expected)
    {
        Assert.Equal(expected, Options.Parse(new[] { "f", "--out", file }).Format);
    }

    [Fact]
    public void Explicit_format_wins_over_out_extension()
    {
        var o = Options.Parse(new[] { "f", "--out", "x.csv", "--format", "jsonl" });
        Assert.Equal(OutputFormat.JsonLines, o.Format);
    }

    [Fact]
    public void Redact_flag_defaults_to_standard_and_accepts_a_level()
    {
        Assert.Equal(RedactionLevel.Standard, Options.Parse(new[] { "f", "--redact" }).Redaction);
        Assert.Equal(RedactionLevel.Aggressive, Options.Parse(new[] { "f", "--redact=aggressive" }).Redaction);
        Assert.Equal(RedactionLevel.Standard, Options.Parse(new[] { "f", "--redact=standard" }).Redaction);
    }

    [Fact]
    public void Parses_the_remaining_flags()
    {
        var o = Options.Parse(new[]
        {
            "f", "--no-recurse", "--hash", "--local-time",
            "--include", ".pdf, .docx", "--max-files", "10", "--max-bytes", "2048", "--max-text", "500",
        });
        Assert.False(o.Recurse);
        Assert.True(o.Hash);
        Assert.False(o.Utc);
        Assert.Equal(new[] { ".pdf", ".docx" }, o.Include.ToArray());
        Assert.Equal(10, o.MaxFiles);
        Assert.Equal(2048L, o.MaxBytes);
        Assert.Equal(500, o.MaxText);
    }

    [Fact]
    public void Invalid_args_throw()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "f", "--format", "xml" }));   // unknown format
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "f", "--redact=weird" }));    // unknown level
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "f", "--bogus" }));           // unknown option
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "f", "--max-files" }));       // missing value
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "f", "--max-files", "-3" })); // negative
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "f", "--max-bytes", "abc" })); // not a number
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "a", "b" }));                 // two positionals
    }

    // ---- Cli.RunAsync (end to end) ----

    private static string MakeFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scrubkit-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "Email jane@example.com and card 4111111111111111.");
        return dir;
    }

    [Fact]
    public async Task Help_and_version_and_no_args_return_zero()
    {
        Assert.Equal(0, await Cli.RunAsync(new[] { "--help" }));
        Assert.Equal(0, await Cli.RunAsync(new[] { "-v" }));
        Assert.Equal(0, await Cli.RunAsync(Array.Empty<string>()));
    }

    [Fact]
    public async Task Unknown_command_and_bad_usage_return_one()
    {
        Assert.Equal(1, await Cli.RunAsync(new[] { "frobnicate" }));
        Assert.Equal(1, await Cli.RunAsync(new[] { "scan" }));                            // no folder
        Assert.Equal(1, await Cli.RunAsync(new[] { "scan", "x", "--format", "parquet" })); // parquet needs --out
        Assert.Equal(1, await Cli.RunAsync(new[] { "scan", "x", "--nope" }));             // bad option
    }

    [Fact]
    public async Task Missing_folder_returns_one()
    {
        var missing = Path.Combine(Path.GetTempPath(), "scrubkit-nope-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(1, await Cli.RunAsync(new[] { "scan", missing }));
    }

    [Fact]
    public async Task Scan_writes_redacted_jsonl_to_a_file()
    {
        var dir = MakeFolder();
        var outFile = Path.Combine(dir, "out.jsonl");
        try
        {
            var code = await Cli.RunAsync(new[] { "scan", dir, "--redact", "--out", outFile });
            Assert.Equal(0, code);

            var lines = File.ReadAllLines(outFile);
            var line = Assert.Single(lines);
            Assert.Contains("[EMAIL]", line);
            Assert.Contains("[CARD]", line);
            Assert.DoesNotContain("jane@example.com", line);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_writes_parquet_file()
    {
        var dir = MakeFolder();
        var outFile = Path.Combine(dir, "out.parquet");
        try
        {
            var code = await Cli.RunAsync(new[] { "scan", dir, "--format", "parquet", "--out", outFile });
            Assert.Equal(0, code);
            Assert.True(new FileInfo(outFile).Length > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
