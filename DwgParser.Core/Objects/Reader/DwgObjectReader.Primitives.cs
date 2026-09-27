using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    public static void DumpObjectHeads(byte[] objectsData, byte[]? handlesData, int count)
    {
        var offsets = new List<int>();
        var handles = new List<ulong>();
        bool haveMap = handlesData is { Length: > 4 } && TryReadHandleOffsets(handlesData, offsets, handles);
        Console.WriteLine($"haveMap={haveMap} entries={offsets.Count}");
        int n = Math.Min(count, offsets.Count);
        for (int i = 0; i < n; i++)
        {
            int off = offsets[i];
            uint ms = ReadMsRaw(objectsData, ref off);
            int dataStart = off;
            var br = new BitReader(objectsData, dataStart);
            short type = br.ReadBS();
            uint bitsize = br.ReadRL();
            int end = i + 1 < n ? offsets[i + 1] : dataStart + (int)ms;
            Console.WriteLine($"[{i}] hdl={handles[i]:X} off={dataStart} ms={ms} nextOff-end={(end - dataStart)} type={type} bitsize={bitsize} head=" + Convert.ToHexString(objectsData, dataStart, Math.Min(16, objectsData.Length - dataStart)));
        }
    }

    public static bool TestWalk(byte[] data, List<int> offsets, List<ulong> handles)
        => TryReadHandleOffsets(data, offsets, handles);

    public static bool TryReadHandleOffsets(byte[] data, List<int> offsets, List<ulong> handles)
    {
        try
        {
            int pos = 0;
            int guard = 0;
            while (pos + 2 <= data.Length && guard++ < 100000)
            {
                int sectionSize = (data[pos] << 8) | data[pos + 1];
                pos += 2;
                if (sectionSize == 0 || sectionSize > 2040 || pos + sectionSize > data.Length)
                    break;
                ulong lastHandle = 0;
                long lastOffset = 0;
                int pageEnd = pos + sectionSize - 2;
                while (pos < pageEnd)
                {
                    uint handleOff = ReadUmcRaw(data, ref pos);
                    if (handleOff == 0)
                        break;
                    int objOffset = ReadMcRaw(data, ref pos);
                    lastHandle += handleOff;
                    lastOffset += objOffset;
                    if (lastOffset <= 0 || lastOffset > int.MaxValue)
                        break;
                    offsets.Add((int)lastOffset);
                    handles.Add(lastHandle);
                }
                pos = pageEnd + 2;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException)
        {
        }
        return offsets.Count > 0;
    }

    internal static bool TrySkipEed(BitReader br)
    {
        int guard = 0;
        while (guard++ < 4096)
        {
            int size = br.ReadBS();
            if (size < 0)
                return false;
            if (size == 0)
                break;
            br.ReadHandle();
            for (int i = 0; i < size && !br.EndOfStream; i++)
                br.ReadRC();
        }
        return true;
    }

    internal static bool TryReadEed(BitReader br, DwgObjectRecord? record)
    {
        var captured = record == null ? null : new List<DwgObjectRecord.EedField>();
        int guard = 0;
        while (guard++ < 4096)
        {
            long start = br.BitPosition;
            int size = br.ReadBS();
            if (size < 0)
                return false;
            if (size == 0)
            {
                if (record != null)
                {
                    record.EedFields.Clear();
                    if (captured != null) record.EedFields.AddRange(captured);
                }
                return true;
            }

            var application = br.ReadHandle();
            var payload = captured == null ? null : new byte[size];
            for (int i = 0; i < size && !br.EndOfStream; i++)
            {
                byte value = br.ReadRC();
                if (payload != null) payload[i] = value;
            }
            if (br.EndOfStream)
                return false;
            captured?.Add(new DwgObjectRecord.EedField(
                start, (int)(br.BitPosition - start), size, application, payload!));
        }
        return false;
    }

    public static void ReadEncColor(BitReader br, DwgEntity entity, DwgObjectRecord? record = null)
    {
        short index = ReadEncColorIndex(br, out uint trueColor, record,
            out uint alpha, out bool hasAlpha, out string colorName, out string colorBookName);
        uint rgb24 = trueColor & 0xFFFFFF;
        bool isTrueColor = ((trueColor >> 24) & 0xFF) == AciPalette.CmcMethodTrueColor
                           && rgb24 is not 0 and not 0xFFFFFF;
        entity.TrueColor = trueColor;
        entity.HasTrueColor = (record?.EncColor?.HasTrueColor ?? false) || trueColor != 0;
        entity.ColorAlpha = alpha;
        entity.HasColorAlpha = hasAlpha;
        entity.ColorName = colorName;
        entity.ColorBookName = colorBookName;
        entity.ColorIndex = isTrueColor
            ? (short)AciPalette.FromCmcTrueColor(trueColor)
            : index;
    }

    public static short ReadEncColorIndex(BitReader br, out uint trueColor)
        => ReadEncColorIndex(br, out trueColor, null,
            out _, out _, out _, out _);

    private static short ReadEncColorIndex(BitReader br, out uint trueColor,
        DwgObjectRecord? record,
        out uint alpha, out bool hasAlpha, out string colorName, out string colorBookName)
    {
        trueColor = 0;
        alpha = 0;
        hasAlpha = false;
        colorName = string.Empty;
        colorBookName = string.Empty;
        int raw = br.ReadBS();
        int flags = (raw >> 8) & 0xFF;
        short index = (short)(raw & 0x1FF);
        bool hasDirectColorHandle = (flags & 0x40) != 0;
        DwgHandleReference directColorHandle = default;
        bool hasTrueColor = false;
        if ((flags & 0x20) != 0)
        {
            alpha = unchecked((uint)br.ReadBL());
            hasAlpha = true;
        }
        if (hasDirectColorHandle)
            directColorHandle = br.ReadHandle();
        else if ((flags & 0x80) != 0)
        {
            trueColor = unchecked((uint)br.ReadBL());
            hasTrueColor = true;
        }
        if ((flags & 0x41) == 0x41)
            colorName = br.ReadTV();
        if ((flags & 0x42) == 0x42)
            colorBookName = br.ReadTV();
        if (record != null)
        {
            record.EncColor = new DwgObjectRecord.EncColorField(
                (byte)flags, hasAlpha, alpha, hasDirectColorHandle, directColorHandle,
                hasTrueColor, trueColor, colorName, colorBookName);
        }
        return index;
    }


    internal static short ReadCmc(BitReader br, out uint trueColor)
        => ReadCmc(br, out trueColor, out _, out _);

    internal static short ReadCmc(BitReader br, out uint trueColor, out string colorName, out string colorBookName)
    {
        short index = br.ReadBS();
        trueColor = unchecked((uint)br.ReadBL());
        byte flag = br.ReadRC();
        colorName = "";
        colorBookName = "";
        if (flag < 4)
        {
            if ((flag & 1) != 0) colorName = br.ReadTV();
            if ((flag & 2) != 0) colorBookName = br.ReadTV();
        }
        return index;
    }


    private static void MarkBlockRegionExtents(WalkResult result)
    {
        for (int i = 0; i < result.BlockRegions.Count; i++)
        {
            int start = result.BlockRegions[i].EntityListStart;
            int end = i + 1 < result.BlockRegions.Count
                ? result.BlockRegions[i + 1].EntityListStart
                : result.Entities.Count;
            result.BlockRegions[i].EntityCount = Math.Max(0, end - start);
        }
    }

    private static void AssociateVertices(List<DwgEntity> entities)
    {
        var byOwner = new Dictionary<ulong, DwgEntity>();
        foreach (var pl in entities)
            if (pl.Kind == DwgEntityType.Polyline2D && pl.OwnerHandle != 0 && !byOwner.ContainsKey(pl.OwnerHandle))
                byOwner[pl.OwnerHandle] = pl;

        var toRemove = new List<DwgEntity>();
        if (byOwner.Count > 0)
        {
            foreach (var v in entities)
            {
                if (v.Kind != DwgEntityType.Vertex2D)
                    continue;
                if (v.OwnerHandle != 0 && byOwner.TryGetValue(v.OwnerHandle, out var owner))
                {
                    owner.Points.Add(v.Start);
                    owner.Bulges.Add(v.Radius);
                    toRemove.Add(v);
                }
            }
            entities.RemoveAll(toRemove.Contains);
            toRemove.Clear();
        }

        DwgEntity? current = null;
        foreach (var e in entities)
        {
            switch (e.Kind)
            {
                case DwgEntityType.Polyline2D:
                    current = e;
                    break;
                case DwgEntityType.Vertex2D when current != null:
                    current.Points.Add(e.Start);
                    current.Bulges.Add(e.Radius);
                    toRemove.Add(e);
                    break;
                case DwgEntityType.SeqEnd:
                    current = null;
                    break;
            }
        }
        entities.RemoveAll(toRemove.Contains);
    }

    private static uint ReadMsRaw(byte[] data, ref int pos)
    {
        uint result = 0;
        for (int shift = 0; shift <= 15; shift += 15)
        {
            if (pos + 2 > data.Length)
                throw new EndOfStreamException();
            ushort w = (ushort)(data[pos] | (data[pos + 1] << 8));
            pos += 2;
            if ((w & 0x8000) == 0)
            {
                result |= (uint)w << shift;
                return result;
            }
            result |= (uint)(w & 0x7FFF) << shift;
        }
        return result;
    }

    private static uint ReadUmcRaw(byte[] data, ref int pos)
    {
        uint result = 0;
        for (int shift = 0; shift <= 49; shift += 7)
        {
            if (pos >= data.Length)
                throw new EndOfStreamException();
            byte b = data[pos++];
            int boundedShift = Math.Min(shift, 24);
            if ((b & 0x80) == 0)
            {
                result |= (uint)b << boundedShift;
                return result;
            }
            result |= (uint)(b & 0x7F) << boundedShift;
        }
        return result;
    }

    private static int ReadMcRaw(byte[] data, ref int pos)
    {
        uint result = 0;
        bool negative = false;
        for (int shift = 0; shift <= 28; shift += 7)
        {
            if (pos >= data.Length)
                throw new EndOfStreamException();
            byte b = data[pos++];
            if ((b & 0x80) == 0)
            {
                if ((b & 0x40) != 0)
                {
                    negative = true;
                    b &= 0xBF;
                }
                result |= (uint)b << shift;
                return negative ? unchecked((int)(0u - result)) : unchecked((int)result);
            }
            result |= (uint)(b & 0x7F) << shift;
        }
        return unchecked((int)result);
    }
}
