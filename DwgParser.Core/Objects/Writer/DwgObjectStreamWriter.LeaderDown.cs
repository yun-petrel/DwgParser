namespace DwgParser.Core.Objects;

using DwgParser.Core.IO;


public static partial class DwgObjectStreamWriter
{
    private const short TypeLeader = 0x2D;
    private static bool CarryLeaderR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen)
    {
        long ins = rec.LeaderTailInsertBit;
        if (ins <= geoStart || ins >= geoStart + geoLen) return false;
        CopyBits(body, src, geoStart, (int)(ins - geoStart));
        body.WriteBD(0.0);
        body.WriteBD(0.0);
        CopyBits(body, src, ins, (int)(geoStart + geoLen - ins));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LDN h={rec.Handle:X} {rec.SourceVersion}→AC1021 box 插 4 位 @rel{ins - (long)rec.BodyStart * 8}");
        return true;
    }

    private static bool CarryLeaderPreR2010Up(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        long del = rec.LeaderBoxDeleteStartBit;
        int len = rec.LeaderBoxDeleteBitLen;
        if (len <= 0 || del <= geoStart || del + len >= geoStart + geoLen) return false;
        CopyBits(body, src, geoStart, (int)(del - geoStart));
        CopyBits(body, src, del + len, (int)(geoStart + geoLen - del - len));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LDU h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"box 剥 {len} 位 @rel{del - (long)rec.BodyStart * 8}");
        return true;
    }

    private static bool CarryLeaderR2010ToAc1018(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen)
    {
        long ins = rec.LeaderTailInsertBit;
        long end = geoStart + geoLen;
        if (ins <= geoStart || ins >= end - 1) return false;
        CopyBits(body, src, geoStart, (int)(ins - geoStart));
        body.WriteBD(0.0);
        body.WriteBD(0.0);
        CopyBits(body, src, ins, (int)(end - 1 - ins));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LDN18 h={rec.Handle:X} {rec.SourceVersion}→AC1018 box 插 4 位 + 尾剥 1 位 " +
                $"@rel{ins - (long)rec.BodyStart * 8}");
        return true;
    }
}
