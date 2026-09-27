using System.Text;
using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class ProxyObjectAcR2007Plus : ProxyObjectRecord
{
    public ProxyObjectAcR2007Plus(ulong handle, DwgObjectRecord rec, int objStart,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
        : base(handle, rec, objStart, classes) { }

    public override string Family => "ProxyObjectAcR2007Plus";

    protected override bool HasInlineName => false;

    protected override bool RequiresCentralClassName => true;

    protected override bool HasDsData => Rec.SourceVersion >= DwgVersion.AC1027;

    protected override bool TryStartFrame(BitReader br, out long hs)
    {
        if (!Rec.SourceVersion.IsR2010Plus()) return base.TryStartFrame(br, out hs);
        hs = Rec.HandleStreamStartBit;
        if (hs <= 0) Fail(br, "定段:R2010+ 公共路径未给出 HandleStreamStartBit");
        return hs > 0;
    }

    protected override long ReadVersion(BitReader br)
    {
        if (Rec.SourceVersion < DwgVersion.AC1032) return br.ReadBL();
        long dwg = br.ReadBL();
        long maint = br.ReadBL();
        return (maint << 16) + dwg;
    }

    protected override bool TryReadCentralClassName(byte[] data, long dataStart, long marker,
        out string name, out string why)
        => TryReadNameBlock(data, dataStart, marker, out name, out why);

    private static bool TryReadNameBlock(byte[] data, long dataStart, long markerRel,
        out string name, out string why)
    {
        name = string.Empty;
        if (markerRel < 10)
        {
            why = $"{MarkerWhy(markerRel)}(markerRel={markerRel})";
            return false;
        }
        long marker = dataStart + markerRel;
        if (!BitsEqual(data, marker - 5, "01000"))
        {
            why = $"名块末 5 位≠01000(markerRel={markerRel})";
            return false;
        }
        int lenHit = 0;
        for (int n = 1; n <= 31; n++)
        {
            long start = marker - 10 - 16 * n;
            if (start < dataStart) continue;
            if (!BitsEqual(data, start, Convert.ToString(n, 2).PadLeft(5, '0'))) continue;
            lenHit++;
            var sb = new StringBuilder(n);
            bool ok = true;
            for (int i = 0; i < n; i++)
            {
                long p = start + 5 + i * 16;
                int c = ReadBits(data, p, 8) | (ReadBits(data, p + 8, 8) << 8);
                if (c is < 32 or > 126) { ok = false; break; }
                sb.Append((char)c);
            }
            if (!ok) continue;
            name = sb.ToString();
            why = "";
            return true;
        }
        why = lenHit == 0
            ? $"marker 前无名块(长度位 1..31 全不自洽, markerRel={markerRel})"
            : $"长度位命中 {lenHit} 处、其字符全越 ASCII 可见区(markerRel={markerRel})";
        return false;
    }

    private static bool BitsEqual(byte[] data, long from, string expect)
    {
        for (int i = 0; i < expect.Length; i++)
            if (BitAt(data, from + i) != (expect[i] == '1')) return false;
        return true;
    }

    private static int ReadBits(byte[] data, long from, int count)
    {
        int v = 0;
        for (int i = 0; i < count; i++) v = (v << 1) | (BitAt(data, from + i) ? 1 : 0);
        return v;
    }

    private static bool BitAt(byte[] data, long bit)
    {
        long idx = bit >> 3;
        if (idx < 0 || idx >= data.Length) return false;
        return ((data[idx] >> (int)(7 - (bit & 7))) & 1) != 0;
    }
}
