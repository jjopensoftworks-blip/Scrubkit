using System.Text;

namespace Scrubkit;

/// <summary>
/// A minimal reader for the OLE2 / Compound File Binary Format (CFBF) — the container
/// Outlook <c>.msg</c> files use. Just enough to enumerate the top-level streams and read
/// their bytes: it parses the header, FAT, directory, and (for small streams) the mini-FAT
/// stream. Not a general OLE implementation; storages beyond the root are flattened by name.
/// </summary>
internal sealed class CompoundFile
{
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FreeSect = 0xFFFFFFFF;

    private readonly Dictionary<string, byte[]> _streams =
        new(StringComparer.Ordinal);

    public CompoundFile(byte[] data)
    {
        if (data.Length < 512 || BitConverter.ToUInt64(data, 0) != 0xE11AB1A1E011CFD0)
            throw new InvalidDataException("Not a compound (OLE2) file.");

        var sectorShift = BitConverter.ToUInt16(data, 30);
        var sectorSize = 1 << sectorShift;                       // 512 (v3) or 4096 (v4)
        var miniSectorSize = 1 << BitConverter.ToUInt16(data, 32);
        var miniCutoff = BitConverter.ToUInt32(data, 56);
        var firstDirSector = BitConverter.ToUInt32(data, 48);
        var firstMiniFat = BitConverter.ToUInt32(data, 60);
        var numDifat = BitConverter.ToUInt32(data, 72);
        var firstDifat = BitConverter.ToUInt32(data, 68);

        int Offset(uint sector) => (int)((sector + 1) * (uint)sectorSize);

        // ---- FAT: gather its sector locations from the header DIFAT (+ any DIFAT sectors). ----
        var fatSectors = new List<uint>();
        for (var i = 0; i < 109; i++)
        {
            var s = BitConverter.ToUInt32(data, 76 + i * 4);
            if (s != FreeSect && s != EndOfChain) fatSectors.Add(s);
        }
        var difat = firstDifat;
        var difatGuard = 0;
        while (numDifat > 0 && difat != EndOfChain && difat != FreeSect && difatGuard++ < data.Length)
        {
            var baseOff = Offset(difat);
            var entries = sectorSize / 4;
            for (var i = 0; i < entries - 1; i++)
            {
                var s = BitConverter.ToUInt32(data, baseOff + i * 4);
                if (s != FreeSect && s != EndOfChain) fatSectors.Add(s);
            }
            difat = BitConverter.ToUInt32(data, baseOff + (entries - 1) * 4);
        }

        var fat = ReadFat(data, fatSectors, sectorSize, Offset);

        // ---- Directory: one linear pass over every 128-byte entry in the chain. ----
        var dir = ReadChain(data, fat, firstDirSector, sectorSize, Offset);
        var miniFatBytes = ReadChain(data, fat, firstMiniFat, sectorSize, Offset);
        var miniFat = ToUintArray(miniFatBytes);

        // The root entry's stream is the mini-stream container (holds all sub-cutoff streams).
        var (rootStart, rootSize) = ReadDirEntryLocation(dir, 0);
        var miniStream = Truncate(ReadChain(data, fat, rootStart, sectorSize, Offset), rootSize);

        var count = dir.Length / 128;
        for (var e = 0; e < count; e++)
        {
            var baseOff = e * 128;
            var type = dir[baseOff + 66];                        // 2 = stream
            if (type != 2) continue;

            var nameLen = BitConverter.ToUInt16(dir, baseOff + 64);
            if (nameLen <= 2) continue;
            var name = Encoding.Unicode.GetString(dir, baseOff, nameLen - 2);   // drop null terminator
            var (start, size) = ReadDirEntryLocation(dir, e);

            byte[] bytes = size < miniCutoff
                ? ReadMiniChain(miniStream, miniFat, start, miniSectorSize, (int)size)
                : Truncate(ReadChain(data, fat, start, sectorSize, Offset), size);

            _streams[name] = bytes;   // flatten by name; last one wins on a dup
        }
    }

    /// <summary>Read a top-level stream by its exact CFBF name, if present.</summary>
    public bool TryRead(string name, out byte[] bytes) => _streams.TryGetValue(name, out bytes!);

    // ---- helpers ----

    private static uint[] ReadFat(byte[] data, List<uint> fatSectors, int sectorSize, Func<uint, int> offset)
    {
        var fat = new List<uint>(fatSectors.Count * (sectorSize / 4));
        foreach (var s in fatSectors)
        {
            var baseOff = offset(s);
            for (var i = 0; i < sectorSize / 4; i++)
                fat.Add(BitConverter.ToUInt32(data, baseOff + i * 4));
        }
        return fat.ToArray();
    }

    private static byte[] ReadChain(byte[] data, uint[] fat, uint start, int sectorSize, Func<uint, int> offset)
    {
        using var ms = new MemoryStream();
        var sector = start;
        var guard = 0;
        while (sector != EndOfChain && sector != FreeSect && sector < fat.Length && guard++ <= fat.Length)
        {
            ms.Write(data, offset(sector), sectorSize);
            sector = fat[sector];
        }
        return ms.ToArray();
    }

    private static byte[] ReadMiniChain(byte[] miniStream, uint[] miniFat, uint start, int miniSectorSize, int size)
    {
        using var ms = new MemoryStream();
        var sector = start;
        var guard = 0;
        while (sector != EndOfChain && sector != FreeSect && sector < miniFat.Length && guard++ <= miniFat.Length)
        {
            var off = (int)sector * miniSectorSize;
            if (off + miniSectorSize > miniStream.Length) break;
            ms.Write(miniStream, off, miniSectorSize);
            sector = miniFat[sector];
        }
        return Truncate(ms.ToArray(), (uint)size);
    }

    private static (uint start, uint size) ReadDirEntryLocation(byte[] dir, int entry)
    {
        var baseOff = entry * 128;
        var start = BitConverter.ToUInt32(dir, baseOff + 116);
        var size = (uint)BitConverter.ToUInt64(dir, baseOff + 120);
        return (start, size);
    }

    private static uint[] ToUintArray(byte[] data)
    {
        var result = new uint[data.Length / 4];
        for (var i = 0; i < result.Length; i++) result[i] = BitConverter.ToUInt32(data, i * 4);
        return result;
    }

    private static byte[] Truncate(byte[] data, uint size)
    {
        if (size >= data.Length) return data;
        var result = new byte[size];
        Array.Copy(data, result, (int)size);
        return result;
    }
}
