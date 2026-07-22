using System.Globalization;
using System.Reflection;
using Scrubkit;

// scrubkit — the Scrubkit command-line tool.
//
//   scrubkit scan <folder> [options]
//
// Walks a folder, extracts text + metadata, optionally redacts PII/secrets, and writes a
// CSV / JSON / JSON Lines / Parquet table. Progress + a summary go to stderr; the table goes
// to stdout (or --out). Fully offline. Exit code 0 on success, 1 on a usage/IO error.

return await Cli.RunAsync(args);

internal static class Cli
{
    // UTF-8 without a BOM — the right default for the machine-readable files we emit
    // (CSV / JSON / JSON Lines / manifest); a leading BOM trips many parsers.
    private static readonly System.Text.Encoding Utf8NoBom = new System.Text.UTF8Encoding(false);

    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine(Version());
            return 0;
        }

        if (!string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            Error($"unknown command '{args[0]}'. Try 'scrubkit --help'.");
            return 1;
        }

        Options opts;
        try
        {
            opts = Options.Parse(args.Skip(1).ToArray());
        }
        catch (ArgumentException ex)
        {
            Error(ex.Message);
            Console.Error.WriteLine("Try 'scrubkit --help'.");
            return 1;
        }

        if (opts.Folder is null)
        {
            Error("no folder given. Usage: scrubkit scan <folder> [options].");
            return 1;
        }

        if (opts.Format == OutputFormat.Parquet && opts.Out is null)
        {
            Error("--format parquet needs --out <file> (Parquet is a binary file, not stdout).");
            return 1;
        }

        return await ScanAsync(opts);
    }

    private static async Task<int> ScanAsync(Options opts)
    {
        var readOptions = new ReadOptions
        {
            Recursion = opts.Recurse ? Recursion.AllNested : Recursion.TopOnly,
            Redaction = opts.Redaction,
            ComputeContentHash = opts.Hash,
        };
        if (opts.MaxFiles is { } mf) readOptions.MaxFiles = mf;
        if (opts.MaxBytes is { } mb) readOptions.MaxBytesPerFile = mb;
        if (opts.MaxText is { } mt) readOptions.MaxTextLength = mt;
        foreach (var ext in opts.Include) readOptions.IncludeExtensions.Add(ext);

        // Never let the scan ingest its own output / manifest if written inside the folder.
        if (opts.Out is not null) readOptions.ExcludePaths.Add(opts.Out);
        if (opts.Manifest is not null) readOptions.ExcludePaths.Add(opts.Manifest);

        // Register every add-on extractor so the CLI handles the whole format family.
        readOptions.Extractors.Add(new EmailExtractor());
        readOptions.Extractors.Add(new OpenDocumentExtractor());
        readOptions.Extractors.Add(new EpubExtractor());

        var scrubber = new FolderScrubber(readOptions);

        IReadOnlyList<FileRecord> records;
        Manifest? manifest = null;
        var removed = 0;
        try
        {
            if (opts.Since is not null)
            {
                // Incremental: extract only what changed since the baseline manifest.
                var baseline = LoadBaseline(opts.Since);
                var result = await scrubber.ReadChangesAsync(opts.Folder!, baseline);
                records = result.Changed;
                removed = result.Removed.Count;
                manifest = result.Manifest;   // complete (changed + carried-forward unchanged)
            }
            else
            {
                var list = new List<FileRecord>();
                await foreach (var record in scrubber.ReadStreamAsync(opts.Folder!))
                {
                    list.Add(record);
                    if (list.Count % 50 == 0)
                        Console.Error.Write($"\r  scanned {list.Count} files…");
                }
                records = list;
                if (opts.Manifest is not null) manifest = Manifest.From(records);
            }
        }
        catch (DirectoryNotFoundException)
        {
            Error($"folder not found: {opts.Folder}");
            return 1;
        }

        try
        {
            await WriteAsync(records, opts);
            if (opts.Manifest is not null && manifest is not null)
            {
                using var mw = new StreamWriter(opts.Manifest, append: false, Utf8NoBom);
                manifest.Save(mw);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error($"could not write output: {ex.Message}");
            return 1;
        }

        var redactionTotal = records.Sum(r => r.Redactions.Values.Sum());
        var withWarnings = records.Count(r => r.Warnings.Count > 0);
        var label = opts.Since is null ? "file(s)" : "changed file(s)";
        Console.Error.Write("\r");   // clear the progress line
        Console.Error.WriteLine(
            $"Done: {records.Count} {label}, {redactionTotal} value(s) redacted, {withWarnings} with warnings" +
            (opts.Since is null ? "" : $", {removed} removed") +
            (opts.Out is null ? "." : $" → {opts.Out}.") +
            (opts.Manifest is not null ? $" Manifest → {opts.Manifest}." : ""));
        return 0;
    }

    // Load the baseline manifest for an incremental run. A missing file is not an error — it
    // means "first run": treat every file as new.
    private static Manifest LoadBaseline(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"scrubkit: no baseline at '{path}' — treating every file as new.");
            return Manifest.Empty;
        }
        using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
        return Manifest.Load(reader);
    }

    private static async Task WriteAsync(IReadOnlyList<FileRecord> records, Options opts)
    {
        if (opts.Format == OutputFormat.Parquet)
        {
            await ParquetTableWriter.WriteFileAsync(records, opts.Out!);
            return;
        }

        TextWriter writer = opts.Out is null
            ? Console.Out
            : new StreamWriter(opts.Out, append: false, Utf8NoBom);
        try
        {
            switch (opts.Format)
            {
                case OutputFormat.Csv: TableWriter.WriteCsv(records, writer, opts.Utc); break;
                case OutputFormat.Json: TableWriter.WriteJson(records, writer, opts.Utc); break;
                default: TableWriter.WriteJsonLines(records, writer, opts.Utc); break;   // JsonLines
            }
        }
        finally
        {
            if (opts.Out is not null) writer.Dispose();   // don't close Console.Out
        }
    }

    private static bool IsHelp(string arg) => arg is "--help" or "-h" or "-?" or "help";

    private static void Error(string message) => Console.Error.WriteLine($"scrubkit: {message}");

    private static string Version() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    private static void PrintHelp()
    {
        Console.WriteLine(
$@"scrubkit {Version()} — offline text + metadata extraction with PII/secret scrubbing

USAGE
  scrubkit scan <folder> [options]

OPTIONS
  --format <fmt>        Output format: csv, json, jsonl, parquet.
                        Default: inferred from --out's extension, else csv.
  --out <file>          Write to a file instead of stdout. Required for parquet.
  --redact[=<level>]    Redact PII + secrets. Level: standard (default) or aggressive.
                        Omit the flag to extract without redacting.
  --no-recurse          Only the top folder (default: recurse all nested folders).
  --hash                Compute a SHA-256 content hash per file.
  --include <exts>      Comma-separated extension filter, e.g. --include .pdf,.docx
  --since <manifest>    Incremental scan: only output files changed since <manifest>
                        (a missing file = first run). Skips unchanged files.
  --manifest <file>     Write a manifest of this scan to <file> (for a later --since).
  --max-files <n>       Stop after n files (0 = no limit).
  --max-bytes <n>       Skip files larger than n bytes (0 = no limit).
  --max-text <n>        Clip extracted text to n characters (0 = no clip).
  --local-time          Emit local-time timestamps (with offset) instead of UTC.
  -h, --help            Show this help.
  -v, --version         Show the version.

EXAMPLES
  scrubkit scan ./docs
  scrubkit scan ./docs --redact --format jsonl --out docs.jsonl
  scrubkit scan ./docs --redact=aggressive --include .pdf,.eml --hash
  scrubkit scan ./data --format parquet --out data.parquet
  scrubkit scan ./docs --since state.txt --manifest state.txt --out delta.jsonl

Everything runs on your machine — no network calls. Best-effort scrubbing, not a
compliance tool.");
    }
}

public enum OutputFormat { Csv, Json, JsonLines, Parquet }

internal sealed class Options
{
    public string? Folder { get; private set; }
    public bool Recurse { get; private set; } = true;
    public RedactionLevel Redaction { get; private set; } = RedactionLevel.Off;
    public OutputFormat Format { get; private set; } = OutputFormat.Csv;
    public string? Out { get; private set; }
    public bool Hash { get; private set; }
    public bool Utc { get; private set; } = true;
    public string? Since { get; private set; }
    public string? Manifest { get; private set; }
    public List<string> Include { get; } = new();
    public int? MaxFiles { get; private set; }
    public long? MaxBytes { get; private set; }
    public int? MaxText { get; private set; }

    public static Options Parse(string[] args)
    {
        var opts = new Options();
        var formatExplicit = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var (key, inlineValue) = SplitInline(arg);

            switch (key)
            {
                case "--format":
                    opts.Format = ParseFormat(Value(key, inlineValue, args, ref i));
                    formatExplicit = true;
                    break;
                case "--out":
                    opts.Out = Value(key, inlineValue, args, ref i);
                    break;
                case "--redact":
                    opts.Redaction = inlineValue is null
                        ? RedactionLevel.Standard
                        : ParseLevel(inlineValue);
                    break;
                case "--no-recurse":
                    opts.Recurse = false;
                    break;
                case "--recurse":
                    opts.Recurse = true;
                    break;
                case "--hash":
                    opts.Hash = true;
                    break;
                case "--local-time":
                    opts.Utc = false;
                    break;
                case "--since":
                    opts.Since = Value(key, inlineValue, args, ref i);
                    break;
                case "--manifest":
                    opts.Manifest = Value(key, inlineValue, args, ref i);
                    break;
                case "--include":
                    foreach (var ext in Value(key, inlineValue, args, ref i).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        opts.Include.Add(ext);
                    break;
                case "--max-files":
                    opts.MaxFiles = ParseInt(key, Value(key, inlineValue, args, ref i));
                    break;
                case "--max-bytes":
                    opts.MaxBytes = ParseLong(key, Value(key, inlineValue, args, ref i));
                    break;
                case "--max-text":
                    opts.MaxText = ParseInt(key, Value(key, inlineValue, args, ref i));
                    break;
                default:
                    if (key.StartsWith('-'))
                        throw new ArgumentException($"unknown option '{key}'.");
                    if (opts.Folder is not null)
                        throw new ArgumentException($"unexpected extra argument '{arg}'.");
                    opts.Folder = arg;
                    break;
            }
        }

        // Infer format from the output file's extension when not given explicitly.
        if (!formatExplicit && opts.Out is not null)
            opts.Format = InferFormat(opts.Out) ?? opts.Format;

        return opts;
    }

    private static (string Key, string? Inline) SplitInline(string arg)
    {
        var eq = arg.IndexOf('=');
        return eq < 0 ? (arg, null) : (arg[..eq], arg[(eq + 1)..]);
    }

    // Value for an option: the inline "=value", else the next token.
    private static string Value(string key, string? inline, string[] args, ref int i)
    {
        if (inline is not null) return inline;
        if (i + 1 >= args.Length) throw new ArgumentException($"option '{key}' needs a value.");
        return args[++i];
    }

    private static OutputFormat ParseFormat(string value) => value.ToLowerInvariant() switch
    {
        "csv" => OutputFormat.Csv,
        "json" => OutputFormat.Json,
        "jsonl" or "ndjson" => OutputFormat.JsonLines,
        "parquet" => OutputFormat.Parquet,
        _ => throw new ArgumentException($"unknown format '{value}'. Use csv, json, jsonl, or parquet."),
    };

    private static OutputFormat? InferFormat(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".csv" => OutputFormat.Csv,
        ".json" => OutputFormat.Json,
        ".jsonl" or ".ndjson" => OutputFormat.JsonLines,
        ".parquet" => OutputFormat.Parquet,
        _ => null,
    };

    private static RedactionLevel ParseLevel(string value) => value.ToLowerInvariant() switch
    {
        "standard" => RedactionLevel.Standard,
        "aggressive" => RedactionLevel.Aggressive,
        "off" => RedactionLevel.Off,
        _ => throw new ArgumentException($"unknown redaction level '{value}'. Use standard or aggressive."),
    };

    private static int ParseInt(string key, string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0
            ? n
            : throw new ArgumentException($"option '{key}' needs a non-negative integer, got '{value}'.");

    private static long ParseLong(string key, string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0
            ? n
            : throw new ArgumentException($"option '{key}' needs a non-negative integer, got '{value}'.");
}
