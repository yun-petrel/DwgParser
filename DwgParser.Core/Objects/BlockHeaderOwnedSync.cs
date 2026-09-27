using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static class BlockHeaderOwnedSync
{
    private const short TypeBlockHeader = 0x31;

    public static (int Headers, int Removed) RemoveFromDecodedVectors(
        DwgObjectReader.WalkResult walk, ulong handle)
    {
        int headers = 0, removed = 0;
        foreach (var bh in walk.Records)
        {
            if (bh.LogicalType != TypeBlockHeader || bh.BlockOwnedHandles is null) continue;
            int n = bh.BlockOwnedHandles.RemoveAll(h => h == handle);
            if (n == 0) continue;
            headers++; removed += n;
        }
        return (headers, removed);
    }

    public static (int Headers, int Attached) AttachToDecodedVectors(
        DwgObjectReader.WalkResult walk, IReadOnlyList<ulong> handles)
    {
        if (handles.Count == 0) return (0, 0);
        DwgObjectRecord? host = ChooseHost(walk, handles);
        if (host is null) return (0, 0);
        int attached = 0;
        foreach (ulong h in handles)
        {
            if (host.BlockOwnedHandles!.Contains(h)) continue;
            host.BlockOwnedHandles.Add(h);
            attached++;
        }
        return (1, attached);
    }

    public static bool AttachToOwnerVector(DwgObjectReader.WalkResult walk, ulong handle, ulong owner)
    {
        if (owner == 0) return false;
        DwgObjectRecord? host = null;
        foreach (var bh in walk.Records)
        {
            if (bh.LogicalType != TypeBlockHeader || bh.BlockOwnedHandles is not { } vector) continue;
            if (vector.Contains(handle)) return true;
            if (bh.Handle == owner) host = bh;
        }
        if (host is null) return false;
        host.BlockOwnedHandles!.Add(handle);
        return true;
    }

    private static DwgObjectRecord? ChooseHost(DwgObjectReader.WalkResult walk, IReadOnlyList<ulong> handles)
    {
        DwgObjectRecord? maxH = null, model = null, owning = null;
        foreach (var bh in walk.Records)
        {
            if (bh.LogicalType != TypeBlockHeader || bh.BlockOwnedHandles is null) continue;
            if (maxH is null || bh.Handle > maxH.Handle) maxH = bh;
            if (walk.ModelSpaceHandle != 0 && bh.Handle == walk.ModelSpaceHandle) model = bh;
            if (owning is null && handles.Any(h => bh.BlockOwnedHandles.Contains(h))) owning = bh;
        }
        return owning ?? model ?? maxH;
    }

    public static bool TryApplyToFrameBits(byte[] src, DwgObjectRecord rec, ulong handle)
    {
        if (src is null || src.Length == 0) return false;
        if (rec.LogicalType != TypeBlockHeader) return false;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || !rec.HasHandleStream) return false;
        long end = (long)rec.BodyEnd * 8;
        if (end <= hs || end > (long)src.Length * 8) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, hs);
            for (long hp = hs; hp + 8 <= end; )
            {
                DwgObjectRecord.SeekTo(br, hp);
                int cc = br.ReadRC();
                int nbytes = cc & 0xF;
                int code = cc >> 4;
                int bits = 8 + nbytes * 8;
                if (hp + bits > end) break;
                if (code == 0 && nbytes == 0) break;
                if (nbytes > 0)
                {
                    ulong v = 0;
                    for (int i = 0; i < nbytes; i++) v = (v << 8) | br.ReadRC();
                    if (v == handle)
                    {
                        for (long b = hp; b < hp + bits && b < (long)src.Length * 8; b++)
                            WriteBit(src, b, false);
                        WriteByte(src, hp, 0x30);
                        return true;
                    }
                }
                hp += bits;
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException or ArgumentException)
        {
            return false;
        }
        return false;
    }

    public static int ChainHeadRefWidth(byte[] src, DwgObjectRecord btr)
    {
        var refs = TryParseChainHeadRefs(src, btr, out _, out _);
        return refs is null ? 0 : refs[4].n;
    }

    private static List<(long bit, int code, int n)>? TryParseChainHeadRefs(
        byte[] src, DwgObjectRecord btr, out ulong first, out ulong last)
    {
        first = last = 0;
        if (src is null || src.Length == 0 || btr.LogicalType != TypeBlockHeader) return null;
        long end = (long)btr.BodyEnd * 8;
        if (end <= 0 || end > (long)src.Length * 8) return null;
        if (!DwgObjectStreamWriter.TryBlockHeaderHandleStreamStart(btr, src, out long hs)
            || hs <= 0 || end <= hs) return null;

        var refs = new List<(long bit, int code, int n)>();
        try
        {
            var br = new BitReader(src, 0);
            for (long p = hs; p + 8 <= end && refs.Count <= 12; )
            {
                DwgObjectRecord.SeekTo(br, p);
                int cc = br.ReadRC(), code = cc >> 4, n = cc & 0xF;
                if (p + 8 + n * 8 > end) break;
                refs.Add((p, code, n));
                if (code == 0 && n == 0) break;
                p += 8 + n * 8;
            }
            if (refs.Count < 7) return null;
            int[] want = { 4, 3, 5, 3, 4, 4, 3 };
            for (int i = 0; i < want.Length; i++)
                if (refs[i].code != want[i]) return null;
            var rd = new BitReader(src, 0);
            first = ReadRefValue(refs[4], rd, src);
            last = ReadRefValue(refs[5], rd, src);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                     or ArgumentException)
        {
            return null;
        }
        return refs;
    }

    private static ulong ReadRefValue((long bit, int code, int n) r, BitReader rd, byte[] src)
    {
        DwgObjectRecord.SeekTo(rd, r.bit + 8);
        ulong v = 0;
        for (int i = 0; i < r.n; i++) v = (v << 8) | rd.ReadRC();
        return v;
    }

    public static ulong? TryRetargetChainHead(byte[] src, DwgObjectRecord btr, ulong newHandle)
    {
        var refs = TryParseChainHeadRefs(src, btr, out ulong old, out _);
        if (refs is null) return null;
        var (fb, _, fn) = refs[4];
        if (fn == 0 || newHandle >> (fn * 8) != 0) return null;
        if (old == 0 || old == newHandle) return null;
        for (int i = 0; i < fn; i++)
            WriteByte(src, fb + 8 + i * 8, (byte)(newHandle >> ((fn - 1 - i) * 8)));
        return old;
    }

    private static void WriteBit(byte[] buf, long bit, bool value)
    {
        int b = (int)(bit >> 3);
        if (b < 0 || b >= buf.Length) return;
        int m = 7 - (int)(bit & 7);
        if (value) buf[b] |= (byte)(1 << m);
        else buf[b] &= (byte)~(1 << m);
    }

    private static void WriteByte(byte[] buf, long bit, byte value)
    {
        for (int i = 0; i < 8; i++)
            WriteBit(buf, bit + i, ((value >> (7 - i)) & 1) != 0);
    }
}
