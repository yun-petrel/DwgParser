using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{



    public static WalkResult ReadObjects(byte[] objectsData, byte[]? handlesData,
        DwgVersion version = DwgVersion.AC1018, ClassTypeCodes.TypeNameResolver? typeName = null,
        System.Text.Encoding? legacyEncoding = null,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
    {
        var pairs = new List<(ulong Handle, int Offset)>();
        if (objectsData is null || objectsData.Length == 0)
            return new WalkResult();
        var offsets = new List<int>();
        var handles = new List<ulong>();
        bool haveMap = false;
        if (handlesData is { Length: > 4 })
            haveMap = TryReadHandleOffsets(handlesData, offsets, handles);
        if (!haveMap)
        {
            int pos = 0;
            while (pos + 2 <= objectsData.Length)
            {
                uint size = ReadMsRaw(objectsData, ref pos);
                if (size == 0 || pos + size > objectsData.Length)
                    break;
                offsets.Add(pos);
                handles.Add(0);
                pos += (int)size + 2;
            }
        }
        pairs.AddRange(offsets.Zip(handles, (o, h) => ((ulong Handle, int Offset))(h, o)));
        return Walk(objectsData, pairs, version, typeName, legacyEncoding, classes);
    }

    public static WalkResult ReadObjectsWithPairs(byte[] objectsData,
        IEnumerable<(ulong Handle, int Offset)> pairs, DwgVersion version,
        ClassTypeCodes.TypeNameResolver? typeName = null,
        System.Text.Encoding? legacyEncoding = null,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
        => Walk(objectsData, pairs.ToList(), version, typeName, legacyEncoding, classes);

    public static WalkResult ReadObjectsR2000(
        byte[] rawFile, IReadOnlyList<ulong> handles, IReadOnlyList<int> offsets,
        DwgVersion version = DwgVersion.AC1015, ClassTypeCodes.TypeNameResolver? typeName = null,
        System.Text.Encoding? legacyEncoding = null,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
    {
        var pairs = new List<(ulong Handle, int Offset)>();
        int n = Math.Min(handles.Count, offsets.Count);
        for (int i = 0; i < n; i++)
        {
            int off = offsets[i];
            if (off > 0 && off < rawFile.Length - 2)
                pairs.Add((handles[i], off));
        }
        return Walk(rawFile, pairs, version, typeName, legacyEncoding, classes);
    }

    private static WalkResult Walk(byte[] data, List<(ulong Handle, int Offset)> pairs, DwgVersion version,
        ClassTypeCodes.TypeNameResolver? typeName = null, System.Text.Encoding? legacyEncoding = null,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
    {
        var result = new WalkResult { SourceData = data };
        result.Classes ??= classes;
        var seenOffsets = new HashSet<int>();
        foreach (var (handle, off) in pairs)
        {
            if (off <= 0 || off >= data.Length - 2)
                continue;
            seenOffsets.Add(off);
            result.ObjectsSeen++;
            try
            {
                if (DecodeObject(data, off, handle, result, version, typeName, legacyEncoding))
                    result.ObjectsDecoded++;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException or OverflowException)
            {
                result.Errors++;
                if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                    System.Console.Error.WriteLine($"DECERR h={handle:X} off={off} {ex.GetType().Name}");
            }
        }

        if (version.IsR2004Plus())
        {
            int recordsBeforeRescan = result.Records.Count;
            int entitiesBeforeRescan = result.Entities.Count;
            RescanUnmappedObjects(data, version, seenOffsets, result, typeName, legacyEncoding);
            if (result.Records.Count > recordsBeforeRescan)
            {
                var extra = new List<DwgObjectRecord>(
                    result.Records.GetRange(recordsBeforeRescan, result.Records.Count - recordsBeforeRescan));
                result.Records.RemoveRange(recordsBeforeRescan, extra.Count);
                foreach (var r in extra)
                    result.FrameEnds.Remove(r);
            }

            if (result.Entities.Count > entitiesBeforeRescan)
            {
                var claimed = new HashSet<ulong>();
                foreach (var r in result.Records)
                    if (r.BlockOwnedHandles is { } oh)
                        foreach (var h in oh) claimed.Add(h);
                if (claimed.Count > 0)
                    for (int i = result.Entities.Count - 1; i >= entitiesBeforeRescan; i--)
                        if (!claimed.Contains(result.Entities[i].Handle))
                            result.Entities.RemoveAt(i);
            }
        }

        if (result.ModelSpaceHandle != 0)
        {
            foreach (var e in result.Entities)
            {
                if (e.EntMode == 2 && e.OwnerHandle == 0)
                    e.OwnerHandle = result.ModelSpaceHandle;
            }
        }

        {
            var referenced = new HashSet<ulong>();
            foreach (var e in result.Entities)
            {
                if (e.Kind == DwgEntityType.Insert && e.BlockHeaderHandle != 0)
                    referenced.Add(e.BlockHeaderHandle);
                if (e.DimBlockHandle != 0)
                    referenced.Add(e.DimBlockHandle);
            }

            var live = result.LiveBlockHeaders;
            if (result.ModelSpaceHandle != 0) live.Add(result.ModelSpaceHandle);
            if (result.PaperSpaceHandle != 0) live.Add(result.PaperSpaceHandle);
            foreach (var h in result.BlockControlEntries)
            {
                if (h == 0) continue;
                live.Add(h);
            }
            foreach (var h in referenced)
                live.Add(h);

            foreach (var region in result.BlockRegions)
            {
                if (region.IsAnonymousDimBlock && !referenced.Contains(region.HeaderHandle))
                    live.Remove(region.HeaderHandle);
            }

            int ghostCount = 0;
            foreach (var e in result.Entities)
            {
                if (e.EntMode == 0 && e.OwnerHandle != 0 && !live.Contains(e.OwnerHandle)
                    && result.BlockHeaderByHandle.ContainsKey(e.OwnerHandle))
                {
                    e.IsErased = true;
                    ghostCount++;
                }
            }
        }

        MarkBlockRegionExtents(result);
        AssociateVertices(result.Entities);
        InferLayerColors(result);
        ResolveInsertNames(result);
        FillLinetypeNames(result);
        FillLayerLinetypes(result);
        AssociateTableStyleRows(result);
        ValidateTableControlEntries(result);
        result.RecordsBaselineFingerprint = result.RecordsFingerprint();
        return result;
    }

    private static void FillLayerLinetypes(WalkResult result)
    {
        if (result.LtypeStyles.Count == 0) return;
        foreach (var r in result.Records)
        {
            var cap = r.Layer;
            if (cap is null || cap.ObjectHandles.Count == 0) continue;
            if (!result.LayerNames.ContainsKey(r.Handle)) continue;
            for (int i = cap.ObjectHandles.Count - 1; i >= 0; i--)
                if (result.LtypeStyles.TryGetValue(cap.ObjectHandles[i], out var li) && li.Name.Length > 0)
                {
                    result.LayerLinetypes[r.Handle] = li.Name;
                    break;
                }
        }
    }

    private static void ResolveInsertNames(WalkResult result)
    {
        if (result.BlockHeaderByHandle.Count == 0) return;
        foreach (var e in result.Entities)
        {
            if (e.Kind != DwgEntityType.Insert || e.BlockHeaderHandle == 0) continue;
            if (!string.IsNullOrEmpty(e.Name)) continue;
            if (result.BlockHeaderByHandle.TryGetValue(e.BlockHeaderHandle, out var region))
            {
                string n = !string.IsNullOrEmpty(region.Name) ? region.Name : region.NameGuess;
                if (!string.IsNullOrEmpty(n))
                    e.Name = n;
            }
        }
    }

    private static void FillLinetypeNames(WalkResult result)
    {
        if (result.LtypeStyles.Count == 0) return;
        foreach (var r in result.Records)
        {
            if (!r.HasLtypeSlot) continue;
            ulong h = r.LtypeRef.Resolve(r.Handle);
            if (!result.LtypeStyles.TryGetValue(h, out var li) || li.Name.Length == 0) continue;
            for (int i = 0; i < result.Entities.Count; i++)
                if (result.Entities[i].Handle == r.Handle)
                {
                    result.Entities[i].LtypeName = li.Name;
                    break;
                }
        }
    }

    private static void RescanUnmappedObjects(byte[] data, DwgVersion version,
        HashSet<int> seenOffsets, WalkResult result, ClassTypeCodes.TypeNameResolver? typeName = null,
        System.Text.Encoding? legacyEncoding = null)
    {
        if (seenOffsets.Count == 0)
            return;

        bool r2010Plus = version.IsR2010Plus();

        var covered = new List<(int start, int end)>();
        foreach (int off in seenOffsets)
        {
            int? fe = FrameEndRaw(data, off, r2010Plus);
            if (fe.HasValue)
                covered.Add((off, fe.Value));
        }
        covered.Sort((a, b) => a.start.CompareTo(b.start));

        const int bufStart = 4;
        var gaps = new List<(int start, int end)>();
        if (covered.Count > 0)
        {
            if (bufStart < covered[0].start)
                gaps.Add((bufStart, covered[0].start));
            for (int i = 0; i + 1 < covered.Count; i++)
            {
                int gapStart = covered[i].end;
                int gapEnd = covered[i + 1].start;
                if (gapStart < gapEnd)
                    gaps.Add((gapStart, gapEnd));
            }
            int tailStart = covered[^1].end;
            if (tailStart < data.Length)
                gaps.Add((tailStart, data.Length));
        }

        if (gaps.Count == 0)
            return;

        int rescanCount = 0;
        foreach (var (gapStart, gapEnd) in gaps)
        {
            int pos = gapStart;
            while (pos + 4 < gapEnd)
            {
                int? fe = FrameEndRaw(data, pos, r2010Plus);
                if (!fe.HasValue || fe.Value > gapEnd)
                { pos++; continue; }
                int frameEnd = fe.Value;
                int crcPos = frameEnd - 2;
                if (crcPos + 2 > data.Length)
                { pos++; continue; }
                ushort crcStored = (ushort)(data[crcPos] | (data[crcPos + 1] << 8));
                ushort crcCalc = Crc.Compute16Dwg(data, pos, crcPos - pos);
                if (crcStored != crcCalc)
                { pos++; continue; }
                if (seenOffsets.Contains(pos))
                { pos = frameEnd; continue; }
                result.ObjectsSeen++;
                rescanCount++;
                try
                {
                    if (DecodeObject(data, pos, 0, result, version, typeName, legacyEncoding))
                        result.ObjectsDecoded++;
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException or OverflowException)
                {
                    result.Errors++;
                }
                pos = frameEnd;
            }
        }
    }

    private static int? FrameEndRaw(byte[] data, int off, bool r2010Plus)
    {
        if (off <= 0 || off + 2 > data.Length) return null;
        int p = off;
        uint size;
        try { size = ReadMsRaw(data, ref p); }
        catch { return null; }
        if (size == 0) return null;
        if (r2010Plus)
        {
            int probe = p;
            try { ReadUmcRaw(data, ref probe); }
            catch { return null; }
            p = probe;
        }
        long frameEnd = (long)p + size + 2;
        if (frameEnd > data.Length || frameEnd <= off) return null;
        return (int)frameEnd;
    }

}
