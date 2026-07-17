using Scrubkit;

// Scrubkit Playground
// -------------------
//   dotnet run                       -> generates a small demo folder and extracts it
//   dotnet run -- "C:\path\to\docs"  -> extracts a folder you point it at
//
// Nothing here touches the network. The demo files contain only ordinary sample text.

var (targetPath, isDemo) = ParseArgs(args);

Console.OutputEncoding = System.Text.Encoding.UTF8;
Banner();

if (isDemo)
{
    targetPath = CreateDemoFolder();
    Console.WriteLine($"No folder given — created a demo folder with sample files:\n  {targetPath}\n");
}
else
{
    Console.WriteLine($"Extracting folder: {targetPath}\n");
}

var options = new ReadOptions { Recursion = Recursion.AllNested };
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

Console.WriteLine("\nTip: pass a real folder to extract your own docs — nothing leaves your machine.");
return 0;

// ---------- helpers ----------

static (string path, bool isDemo) ParseArgs(string[] args)
{
    var path = args.FirstOrDefault(a => !a.StartsWith('-'));
    return path is null ? ("", true) : (path, false);
}

static void Banner()
{
    const string title = "Scrubkit Playground — offline file extraction";
    var line = new string('─', title.Length + 4);
    Console.WriteLine($"┌{line}┐");
    Console.WriteLine($"│  {title}  │");
    Console.WriteLine($"└{line}┘\n");
}

static string CreateDemoFolder()
{
    var root = Path.Combine(Path.GetTempPath(), "scrubkit-demo-" + DateTime.Now.ToString("HHmmss"));
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(Path.Combine(root, "handbook"));

    File.WriteAllText(Path.Combine(root, "welcome.txt"),
        "Welcome to the team! This guide walks new members through their first week.\n" +
        "Start with the setup checklist, then say hello in the team channel.");

    File.WriteAllText(Path.Combine(root, "notes.md"),
        "# Onboarding\n\n- Read the handbook\n- Set up your workstation\n- Book a welcome chat with your buddy\n");

    File.WriteAllText(Path.Combine(root, "handbook", "expenses.csv"),
        "category,limit,notes\n" +
        "Travel,1500,Book through the portal\n" +
        "Equipment,1200,Manager approval for laptops\n");

    File.WriteAllText(Path.Combine(root, "service.log"),
        "2026-07-13 12:00:01 service started\n" +
        "2026-07-13 12:00:05 config reloaded\n");

    return root;
}

static void PrintTable(IReadOnlyList<FileRecord> table)
{
    Console.WriteLine($"Found {table.Count} file(s):\n");
    Console.WriteLine($"  {"NAME",-24} {"BUCKET",-12} {"SIZE",8} {"TEXT",8}  WARN");
    Console.WriteLine("  " + new string('─', 64));

    foreach (var r in table)
    {
        var warn = r.Warnings.Count > 0 ? string.Join(";", r.Warnings) : "";
        Console.WriteLine($"  {Trunc(r.Name, 24),-24} {r.TypeBucket,-12} {r.SizeBytes,8} {r.Text.Length,8}  {warn}");
    }
}

static void PrintDetail(IReadOnlyList<FileRecord> table)
{
    var first = table.FirstOrDefault(r => r.Text.Length > 0);
    if (first is null) return;

    Console.WriteLine($"\nText preview — {first.Name}:\n");
    Console.WriteLine("  " + Trunc(first.Text.Replace("\n", " "), 200));
}

static string Trunc(string s, int max) =>
    s.Length <= max ? s : s[..(max - 1)] + "…";
