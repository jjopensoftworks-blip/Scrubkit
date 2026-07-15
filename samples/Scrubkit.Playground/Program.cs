using Scrubkit;

// Scrubkit Playground
// -------------------
//   dotnet run                       -> generates a demo folder with fake sensitive data and scrubs it
//   dotnet run -- "C:\path\to\docs"  -> scrubs a folder you point it at
//   dotnet run -- "C:\docs" --level aggressive
//
// Nothing here touches the network. The demo files contain only fabricated data.

var (targetPath, level, isDemo) = ParseArgs(args);

Console.OutputEncoding = System.Text.Encoding.UTF8;
Banner();

if (isDemo)
{
    targetPath = CreateDemoFolder();
    Console.WriteLine($"No folder given — created a demo folder with fake sensitive data:\n  {targetPath}\n");
}
else
{
    Console.WriteLine($"Scrubbing folder: {targetPath}\n");
}

var options = new ReadOptions
{
    Recursion = Recursion.AllNested,
    Redaction = level,
};

var scrubber = new FolderScrubber(options);

IReadOnlyList<FileRecord> table;
try
{
    table = await scrubber.ReadAsync(targetPath);
}
catch (DirectoryNotFoundException)
{
    Console.Error.WriteLine($"Folder not found: {targetPath}");
    return 1;
}

PrintTable(table);
PrintDetail(table);

if (isDemo)
    Console.WriteLine($"\nTip: run  dotnet run -- \"{targetPath}\" --level aggressive  to see the aggressive pass.");

return 0;

// ---------- helpers ----------

static (string path, RedactionLevel level, bool isDemo) ParseArgs(string[] args)
{
    string? path = null;
    var level = RedactionLevel.Standard;

    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] is "--level" or "-l" && i + 1 < args.Length)
        {
            level = args[++i].ToLowerInvariant() switch
            {
                "off"        => RedactionLevel.Off,
                "aggressive" => RedactionLevel.Aggressive,
                _            => RedactionLevel.Standard,
            };
        }
        else if (!args[i].StartsWith('-'))
        {
            path = args[i];
        }
    }

    return path is null ? ("", level, true) : (path, level, false);
}

static void Banner()
{
    Console.WriteLine("┌───────────────────────────────────────────────┐");
    Console.WriteLine("│  Scrubkit Playground — offline file scrubbing  │");
    Console.WriteLine("└───────────────────────────────────────────────┘\n");
}

static string CreateDemoFolder()
{
    var root = Path.Combine(Path.GetTempPath(), "scrubkit-demo-" + DateTime.Now.ToString("HHmmss"));
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(Path.Combine(root, "invoices"));

    File.WriteAllText(Path.Combine(root, "welcome.txt"),
        "Hi! Reach the support desk at help@contoso.com or call +1 (415) 555-0182.\n" +
        "Your account manager is Dana, dana.lee@contoso.com.");

    File.WriteAllText(Path.Combine(root, "notes.md"),
        "# Onboarding\n\n- Server: 10.0.42.7\n- Backup server: 192.168.1.254\n" +
        "- Escalation SSN on file: 123-45-6789 (do not share)\n");

    File.WriteAllText(Path.Combine(root, "invoices", "invoice-3391.csv"),
        "customer,email,card,amount\n" +
        "Acme Co,ap@acme.example,4111 1111 1111 1111,1299.00\n" +
        "Globex,billing@globex.example,5555 5555 5555 4444,842.50\n");

    File.WriteAllText(Path.Combine(root, "readme.log"),
        "2026-07-13 12:00:01 login user=root ip=203.0.113.9\n" +
        "2026-07-13 12:00:05 contact set to ops@contoso.com\n");

    return root;
}

static void PrintTable(IReadOnlyList<FileRecord> table)
{
    Console.WriteLine($"Found {table.Count} file(s):\n");
    Console.WriteLine($"  {"NAME",-24} {"BUCKET",-12} {"SIZE",8}  {"SCRUBBED",-24} WARN");
    Console.WriteLine("  " + new string('─', 78));

    foreach (var r in table)
    {
        var scrubbed = r.HasSensitiveData
            ? string.Join(", ", r.Redactions.Select(kv => $"{kv.Key}:{kv.Value}"))
            : "—";
        var warn = r.Warnings.Count > 0 ? string.Join(";", r.Warnings) : "";
        Console.WriteLine($"  {Trunc(r.Name, 24),-24} {r.TypeBucket,-12} {r.SizeBytes,8}  {Trunc(scrubbed, 24),-24} {warn}");
    }
}

static void PrintDetail(IReadOnlyList<FileRecord> table)
{
    var first = table.FirstOrDefault(r => r.HasSensitiveData);
    if (first is null) return;

    Console.WriteLine($"\nScrubbed text preview — {first.Name}:\n");
    Console.WriteLine("  " + Trunc(first.Text.Replace("\n", " "), 200));
}

static string Trunc(string s, int max) =>
    s.Length <= max ? s : s[..(max - 1)] + "…";
