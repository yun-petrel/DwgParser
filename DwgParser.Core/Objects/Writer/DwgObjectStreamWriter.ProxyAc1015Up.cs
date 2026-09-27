using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const short TypeProxyObj = 499;

    internal const string ProxyClassNamePrefix = "cn:";

    private static void StampProxyClassNames(DwgObjectReader.WalkResult walk)
    {
        var classes = walk.Classes;
        if (classes is not { Count: > 0 }) return;
        foreach (var rec in walk.Records)
        {
            if (rec.LogicalType != TypeProxyObj || rec.SourceVersion >= DwgVersion.AC1018) continue;
            if (rec.ProxyObject is not { Captured: true, NameBitLen: 0 } cap) continue;
            if (cap.ClassName.Length > 0) continue;
            var cls = classes.FirstOrDefault(c => c.Number == (int)cap.ClassId);
            if (cls is null || cls.CppName.Length == 0) continue;
            cap.ClassName = ProxyClassNamePrefix + cls.CppName;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"PXNAME h={rec.Handle:X} cid={cap.ClassId} → \"{cap.ClassName}\"");
        }
    }

    private static bool TryCopyDataRegionProxyClassNameToR2004(BitWriter body, byte[] src,
        long from, long to, DwgObjectRecord rec, bool rlInBody)
    {
        if (rec.ProxyObject is not { Captured: true, NameBitLen: 0 } cap) return false;
        string name = cap.ClassName;
        if (name.Length is 0 or > 127 || name.Any(c => c > 0x7f)) return false;
        long at = cap.NameStartBit;
        if (at <= from || at >= to) return false;

        long startBit = body.BitPosition;
        CopyBits(body, src, from, (int)(at - from));
        body.WriteTV(name, false);
        CopyBits(body, src, at, (int)(to - at));
        int added = (int)(body.BitPosition - startBit) - (int)(to - from);
        if (rlInBody && added != 0)
        {
            uint rlNow = new BitReader(body.ToArray(), 0).ReadRL();
            body.PatchRL(0, unchecked((uint)(rlNow + added)));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"PXNAME-RL h={rec.Handle:X} RL {rlNow}→{rlNow + added}（补类名 +{added} 位）");
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"PXR2004 h={rec.Handle:X} name=\"{name}\" at=+{at - from} 位 窗=[{from},{to}) +{added}");
        return true;
    }
}
