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
    public void Parses_incremental_options()
    {
        var o = Options.Parse(new[] { "f", "--since", "old.txt", "--manifest", "new.txt" });
        Assert.Equal("old.txt", o.Since);
        Assert.Equal("new.txt", o.Manifest);
    }

    [Fact]
    public async Task Incremental_scan_emits_only_changed_and_writes_a_bom_free_manifest()
    {
        var dir = MakeFolder();   // one file: a.txt
        // Outputs live outside the scanned folder, so a run never re-ingests them.
        var outDir = Path.Combine(Path.GetTempPath(), "scrubkit-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var manifest = Path.Combine(outDir, "state.txt");
        var out1 = Path.Combine(outDir, "d1.jsonl");
        var out2 = Path.Combine(outDir, "d2.jsonl");
        try
        {
            // First run: no baseline yet → everything is "changed", manifest written.
            Assert.Equal(0, await Cli.RunAsync(new[] { "scan", dir, "--since", manifest, "--manifest", manifest, "--out", out1 }));
            Assert.Single(File.ReadAllLines(out1));
            Assert.True(File.Exists(manifest));

            // Manifest must not start with a UTF-8 BOM.
            var bom = new byte[3];
            using (var fs = File.OpenRead(manifest)) { _ = fs.Read(bom, 0, 3); }
            Assert.False(bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF, "manifest should have no BOM");

            // Second run, nothing changed → empty delta.
            Assert.Equal(0, await Cli.RunAsync(new[] { "scan", dir, "--since", manifest, "--out", out2 }));
            Assert.Empty(File.ReadAllText(out2).Trim());

            // Add a file → only it appears in the delta.
            File.WriteAllText(Path.Combine(dir, "b.txt"), "new file with jane@example.com");
            var out3 = Path.Combine(outDir, "d3.jsonl");
            Assert.Equal(0, await Cli.RunAsync(new[] { "scan", dir, "--since", manifest, "--out", out3 }));
            var line = Assert.Single(File.ReadAllLines(out3));
            Assert.Contains("b.txt", line);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public async Task Manifest_written_on_a_full_scan_too()
    {
        var dir = MakeFolder();
        var manifest = Path.Combine(dir, "state.txt");
        try
        {
            Assert.Equal(0, await Cli.RunAsync(new[] { "scan", dir, "--manifest", manifest, "--out", Path.Combine(dir, "o.csv") }));
            var reloaded = Manifest.Load(new StringReader(File.ReadAllText(manifest)));
            Assert.Equal(1, reloaded.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parses_rules_option()
    {
        Assert.Equal("r.json", Options.Parse(new[] { "f", "--rules", "r.json" }).Rules);
    }

    [Fact]
    public async Task Rules_file_applies_custom_rules_and_implies_redaction()
    {
        var dir = MakeFolder();   // a.txt: email + card
        File.WriteAllText(Path.Combine(dir, "note.txt"), "Employee E123456 on duty");
        var aux = Path.Combine(Path.GetTempPath(), "scrubkit-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(aux);
        var rules = Path.Combine(aux, "rules.json");
        var outFile = Path.Combine(aux, "o.jsonl");
        File.WriteAllText(rules, "{\"rules\":[{\"category\":\"EmployeeId\",\"pattern\":\"E[0-9]{6}\",\"token\":\"[EMP]\"}]}");
        try
        {
            var code = await Cli.RunAsync(new[] { "scan", dir, "--rules", rules, "--out", outFile });
            Assert.Equal(0, code);

            var text = File.ReadAllText(outFile);
            Assert.Contains("[EMP]", text);          // custom rule applied
            Assert.DoesNotContain("E123456", text);
            Assert.Contains("[EMAIL]", text);        // --rules implied redaction, so built-ins ran too
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Directory.Delete(aux, recursive: true);
        }
    }

    [Fact]
    public async Task Missing_or_invalid_rules_file_returns_one()
    {
        var dir = MakeFolder();
        var aux = Path.Combine(Path.GetTempPath(), "scrubkit-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(aux);
        var badRegex = Path.Combine(aux, "bad.json");
        File.WriteAllText(badRegex, "{\"rules\":[{\"category\":\"Bad\",\"pattern\":\"(\"}]}");
        try
        {
            Assert.Equal(1, await Cli.RunAsync(new[] { "scan", dir, "--rules", Path.Combine(aux, "missing.json") }));
            Assert.Equal(1, await Cli.RunAsync(new[] { "scan", dir, "--rules", badRegex }));   // invalid regex
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Directory.Delete(aux, recursive: true);
        }
    }

    [Fact]
    public async Task Rules_file_enables_stable_tokens_and_reveal_last()
    {
        var dir = MakeFolder();   // a.txt: "Email jane@example.com and card 4111111111111111."
        var aux = Path.Combine(Path.GetTempPath(), "scrubkit-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(aux);
        var rules = Path.Combine(aux, "rules.json");
        var outFile = Path.Combine(aux, "o.jsonl");
        File.WriteAllText(rules, "{\"stableTokens\":true,\"revealLast\":{\"Card\":4}}");
        try
        {
            var code = await Cli.RunAsync(new[] { "scan", dir, "--rules", rules, "--out", outFile });
            Assert.Equal(0, code);

            var text = File.ReadAllText(outFile);
            Assert.Matches(@"\[EMAIL_[0-9a-f]{8}\]", text);   // stable token
            Assert.Contains("************1111", text);         // format-preserving card mask
            Assert.DoesNotContain("jane@example.com", text);
            Assert.DoesNotContain("4111111111111111", text);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Directory.Delete(aux, recursive: true);
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
