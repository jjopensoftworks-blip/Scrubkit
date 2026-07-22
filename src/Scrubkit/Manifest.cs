using System.Globalization;

namespace Scrubkit;

/// <summary>
/// A lightweight index of the files seen in a scan — one <see cref="ManifestEntry"/> per file —
/// used to run incrementally: on a later scan, files whose size and last-write time still match
/// their manifest entry are unchanged and can be skipped (see
/// <see cref="FolderScrubber.ReadChangesAsync"/>).
///
/// Persisted as a small text sidecar — a header line, then one tab-separated line per file
/// (<c>size · modified-UTC-ticks · hash · path</c>, path last so it may contain spaces). Zero
/// dependencies, like the rest of the core. Paths are compared ordinally.
/// </summary>
public sealed class Manifest
{
    private const string Header = "# scrubkit-manifest v1";
    private readonly Dictionary<string, ManifestEntry> _byPath;

    /// <summary>Creates a manifest from a set of entries (last wins on a duplicate path).</summary>
    public Manifest(IEnumerable<ManifestEntry> entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        _byPath = new Dictionary<string, ManifestEntry>(StringComparer.Ordinal);
        foreach (var e in entries) _byPath[e.Path] = e;
    }

    /// <summary>An empty manifest — the baseline for a first run.</summary>
    public static Manifest Empty { get; } = new(Array.Empty<ManifestEntry>());

    /// <summary>All entries, in path order.</summary>
    public IReadOnlyList<ManifestEntry> Entries =>
        _byPath.Values.OrderBy(e => e.Path, StringComparer.Ordinal).ToList();

    /// <summary>Number of files recorded.</summary>
    public int Count => _byPath.Count;

    /// <summary>Looks up a file's recorded entry by path.</summary>
    public bool TryGet(string path, out ManifestEntry entry) => _byPath.TryGetValue(path, out entry!);

    /// <summary>Builds a manifest from a scan's records (e.g. to persist after a full run).</summary>
    public static Manifest From(IEnumerable<FileRecord> records)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        return new Manifest(records.Select(ManifestEntry.From));
    }

    /// <summary>Writes the manifest to <paramref name="writer"/> as a text sidecar.</summary>
    public void Save(TextWriter writer)
    {
        if (writer is null) throw new ArgumentNullException(nameof(writer));
        writer.Write(Header);
        writer.Write('\n');
        foreach (var e in Entries)
        {
            writer.Write(e.SizeBytes.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(e.Modified.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(e.ContentHash ?? "");
            writer.Write('\t');
            writer.Write(e.Path);
            writer.Write('\n');
        }
    }

    /// <summary>
    /// Reads a manifest previously written by <see cref="Save"/>. Blank / comment lines and any
    /// malformed line are skipped — a corrupt manifest degrades to "reprocess those files"
    /// rather than throwing.
    /// </summary>
    public static Manifest Load(TextReader reader)
    {
        if (reader is null) throw new ArgumentNullException(nameof(reader));
        var entries = new List<ManifestEntry>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split(new[] { '\t' }, 4);   // path last: keep any spaces it contains
            if (parts.Length != 4) continue;
            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) continue;
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)) continue;
            if (ticks < 0 || ticks > DateTime.MaxValue.Ticks) continue;
            entries.Add(new ManifestEntry
            {
                Path = parts[3],
                SizeBytes = size,
                Modified = new DateTime(ticks, DateTimeKind.Utc),
                ContentHash = parts[2].Length == 0 ? null : parts[2],
            });
        }
        return new Manifest(entries);
    }
}
