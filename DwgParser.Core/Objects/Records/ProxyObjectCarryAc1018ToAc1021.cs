using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class ProxyObjectCarryAc1018ToAc1021 : ProxyObjectCarryPlan
{
    public override string Name => "ProxyObject:AC1018→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.ProxyObject is { Captured: true, TailMarkerRelBit: >= 0 } cap
           && cap.ClassName.Length > 0;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.ProxyObject;
        if (cap is not { Captured: true } || cap.TailMarkerRelBit < 0) return false;
        string? tail = rec.ProxyObjectTargetTail;
        if (tail is null || tail.Length != 16) return false;
        if (from != rec.CarryStartBit) return false;

        long dataStart = cap.DataRegionStartBit;
        long hs = cap.HandleStreamStartBit;
        long dataLen = hs - dataStart;
        long k = cap.TailMarkerRelBit;
        if (k <= 0 || k + 16 > dataLen) return false;
        if (cap.NameStartBit < 0 || cap.NameBitLen <= 0) return false;
        int n = cap.ClassName.Length;
        if (n > 31) return false;

        var data = new BitWriter((int)dataLen + 16 * n + 32);
        CopyBits(data, src, dataStart, (int)k);
        data.WriteBits((uint)n, 5);
        foreach (char c in cap.ClassName) data.WriteRS((ushort)c);
        data.WriteBits(0b01000, 5);
        CopyBits(data, src, dataStart + k, (int)(dataLen - k - 16));
        data.WriteBits(System.Convert.ToUInt16(tail, 2), 16);
        int dataBits = (int)data.BitPosition;

        int prefixBits = (int)(cap.NameStartBit - (rec.CarryStartBit + 32));
        int verBits = (int)(cap.HeadEndBit - (cap.NameStartBit + cap.NameBitLen));

        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
        body.WriteRL((uint)(typeBits + 32 + prefixBits + verBits + dataBits));
        CopyBits(body, src, rec.CarryStartBit + 32, prefixBits);
        CopyBits(body, src, cap.NameStartBit + cap.NameBitLen, verBits);
        WriteBitsFromWriter(body, data, dataBits);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"PROXYUP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} k={k} n={n} "
                + $"data {dataLen}→{dataBits} 位 typeBits={typeBits} prefix={prefixBits} ver={verBits} rl={(typeBits + 32 + prefixBits + verBits + dataBits)}");
        return true;
    }

    private static void WriteBitsFromWriter(BitWriter dst, BitWriter src, int bits)
    {
        var span = src.BufferSpan;
        for (int i = 0; i < bits; i++)
            dst.WriteBit((span[i >> 3] >> (7 - (i & 7)) & 1) != 0);
    }
}
