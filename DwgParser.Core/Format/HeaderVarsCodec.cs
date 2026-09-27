using System;
using System.Collections.Generic;
using System.Text;
using DwgParser.Core.IO;

namespace DwgParser.Core.Format;

public static class HeaderVarsCodec
{
    private static readonly string[] Order =
    {
        "R_13b1","R_13b2","R_13","R_13c3","R_14","R_2000b","R_2000","R_2000i",
        "R_2002","R_2004a","R_2004b","R_2004c","R_2004","R_2007a","R_2007b",
        "R_2007","R_2010b","R_2010","R_2013b","R_2013","R_2018b","R_2018"
    };
    private static readonly Dictionary<string, int> VIdx = BuildVIdx();
    private static readonly Dictionary<string, string> Ac2Ver = new()
    {
        ["AC1012"] = "R_13", ["AC1014"] = "R_14", ["AC1015"] = "R_2000", ["AC1018"] = "R_2004",
        ["AC1021"] = "R_2007", ["AC1024"] = "R_2010", ["AC1027"] = "R_2013", ["AC1032"] = "R_2018"
    };

    private static Dictionary<string, int> BuildVIdx()
    {
        var d = new Dictionary<string, int>();
        for (int i = 0; i < Order.Length; i++) d[Order[i]] = i;
        return d;
    }

    private static string AcCode(DwgVersion ac) => $"AC{(ushort)ac:X4}";

    private static readonly byte[] BeginSentinel =
        { 0xcf,0x7b,0x1f,0x23,0xfd,0xde,0x38,0xa9,0x5f,0x7c,0x68,0xb8,0x4e,0x6d,0x33,0x5f };
    private static readonly byte[] EndSentinel = Complement(BeginSentinel);

    private static byte[] Complement(byte[] src)
    {
        var d = new byte[src.Length];
        for (int i = 0; i < src.Length; i++) d[i] = (byte)(src[i] ^ 0xFF);
        return d;
    }

    public readonly record struct HvHandle(int Code, ulong Value);
    public readonly record struct HvCmc(int Index, long Rgb, int Flag, string? Name, string? Book);
    public readonly record struct HvTime(long A, long B);
    public readonly record struct Hv3Bd(double X, double Y, double Z);
    public readonly record struct Hv2Rd(double X, double Y);


    private static bool Active(HvCond[] conds, string ver)
    {
        int vi = VIdx[ver];
        foreach (var c in conds)
        {
            switch (c.Kind)
            {
                case HvCondKind.Since: if (vi < VIdx[c.A!]) return false; break;
                case HvCondKind.Until: if (vi > VIdx[c.A!]) return false; break;
                case HvCondKind.Pre: if (vi >= VIdx[c.A!]) return false; break;
                case HvCondKind.Versions:
                    if (vi < VIdx[c.A!] || vi > VIdx[c.B!]) return false;
                    break;
            }
        }
        return true;
    }

    private static bool IsR7(string ver) => VIdx[ver] >= VIdx["R_2007"];
    private static bool IsR2004(string ver) => VIdx[ver] >= VIdx["R_2004"];

    private static bool CpsnGate(Dictionary<string, object?> values)
    {
        if (!values.TryGetValue("CEPSNTYPE", out var ct)) return false;
        int ce = ct switch
        {
            int i => i,
            short s => s,
            long l => (int)l,
            uint u => (int)u,
            _ => -1
        };
        return ce == 3;
    }

    public static Dictionary<string, object?> Decode(byte[] data, DwgVersion ac)
    {
        string ver = Ac2Ver[AcCode(ac)];
        if (data.Length < 32 || !Matches(data, 0, BeginSentinel))
            throw new InvalidDataException("头部变量节缺少 begin sentinel。");

        var values = new Dictionary<string, object?>();
        var r7 = IsR7(ver);
        var main = new BitReader(data); main.SeekBits(128);
        int size = (int)main.ReadRL();
        BitReader str, hdl;
        if (r7)
        {
            bool hasHi = VIdx[ver] >= VIdx["R_2010"];
            int bitsizeHi = hasHi ? (int)main.ReadRL() : 0;
            int bitsize = (int)main.ReadRL();
            hdl = new BitReader(data);
            hdl.SeekBits(160 + (hasHi ? 32 : 0) + bitsize);
            int sbase = (int)(hdl.BitPosition - 1);
            var sb = new BitReader(data); sb.SeekBits(sbase);
            bool endbit = sb.ReadB();
            int dsz = 0;
            if (endbit)
            {
                var s2 = new BitReader(data); s2.SeekBits(sbase - 16);
                dsz = s2.ReadRS();
                if ((dsz & 0x8000) != 0)
                {
                    var s3 = new BitReader(data); s3.SeekBits(sbase - 32);
                    int hi = s3.ReadRS();
                    dsz = (dsz & 0x7FFF) | (hi << 15);
                }
            }
            str = new BitReader(data); str.SeekBits(sbase - 16 - dsz); str.UnicodeStrings = true;
        }
        else
        {
            str = main;
            hdl = main;
        }

        foreach (var f in HvFieldTable.Fields)
        {
            if (!Active(f.Conds, ver)) continue;
            if (f.Name == "CPSNID" && !CpsnGate(values)) continue;
            if (r7 && (f.Name is "unknown_54" or "unknown_55" or "unknown_56" or "unknown_57"))
                continue;

            object? v;
            try
            {
                v = f.Type switch
                {
                    "B" => main.ReadB(),
                    "BS" or "BSd" => main.ReadBS(),
                    "BL" or "BLx" or "BLd" => main.ReadBL(),
                    "BD" => main.ReadBD(),
                    "RC" => main.ReadRC(),
                    "TV" => main.ReadTV(),
                    "T" => str.ReadTV(),
                    "TIMEBLL" => ReadTimeBll(main),
                    "BLL" => ReadBll(main),
                    "CMC" => ReadCmc(main, r7 ? str : main, r7, IsR2004(ver)),
                    "DH" => ReadHandle(main),
                    "H" => ReadHandle(hdl),
                    "3BD" => Read3Bd(main),
                    "2RD" => Read2Rd(main),
                    _ => throw new NotSupportedException($"未知头部变量类型 {f.Type}")
                };
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"头部变量字段 [{f.Name}]({f.Type}) 解码失败，位位置 main={main.BitPosition} hdl={hdl.BitPosition} r7={r7}", ex);
            }
            values[f.Name] = v;
        }
        return values;
    }

    private static bool Matches(byte[] data, int off, byte[] pat)
    {
        for (int i = 0; i < pat.Length; i++)
            if (data[off + i] != pat[i]) return false;
        return true;
    }

    public static byte[] Encode(Dictionary<string, object?> values, DwgVersion target)
    {
        string ver = Ac2Ver[AcCode(target)];
        var r7 = IsR7(ver);
        bool hasHi = r7 && target >= DwgVersion.AC1024;

        var mw = new BitWriter(); var sw = new BitWriter(); var hw = new BitWriter();
        EncodeMainAndStr(mw, sw, values, ver, measureOnly: true);
        EncodeHandle(hw, values, ver, measureOnly: true);
        long mainBits = mw.BitPosition, strBits = sw.BitPosition, hdlBits = hw.BitPosition;

        long trailerBits = r7 ? 77 : 5;
        long sizeBits = r7 ? R2000ClassTable.SizeFieldBits(strBits) : 0;
        long bitsize = r7 ? 32 + mainBits + strBits + sizeBits + 1 : 0;
        long dataBits = r7
            ? mainBits + strBits + sizeBits + 1 + hdlBits + trailerBits
            : mainBits;
        long dataBytes = (dataBits + 7) / 8;
        long size = (r7 ? 4 : 0) + dataBytes;
        long crcPos = (hasHi ? 24 : 20) + size;

        var final = new BitWriter();
        foreach (var b in BeginSentinel) final.WriteRC(b);
        final.WriteRL((uint)size);
        if (r7)
        {
            if (hasHi) final.WriteRL(0);
            final.WriteRL((uint)bitsize);
        }
        var mw2 = new BitWriter(); var sw2 = new BitWriter(); var hw2 = new BitWriter();
        EncodeMainAndStr(mw2, sw2, values, ver, measureOnly: false);
        EncodeHandle(hw2, values, ver, measureOnly: false);
        final.WriteBitsFrom(mw2, mw2.BitPosition);
        if (r7)
        {
            final.WriteBitsFrom(sw2, sw2.BitPosition);
            R2000ClassTable.WriteSizeField(final, strBits);
            final.WriteB(true);
            final.WriteBitsFrom(hw2, hw2.BitPosition);
            WriteTrailer(final, ver);
        }
        while (final.BitPosition % 8 != 0) final.WriteBit(false);
        final.WriteObjectCRC(16);
        foreach (var b in EndSentinel) final.WriteRC(b);
        return final.ToArray();
    }

    private static void EncodeMainAndStr(BitWriter mainW, BitWriter strW, Dictionary<string, object?> values, string ver, bool measureOnly)
    {
        var r7 = IsR7(ver);
        var r2004 = IsR2004(ver);
        foreach (var f in HvFieldTable.Fields)
        {
            if (!Active(f.Conds, ver)) continue;
            if (f.Name == "CPSNID" && !CpsnGate(values)) continue;
            if (r7 && (f.Name is "unknown_54" or "unknown_55" or "unknown_56" or "unknown_57"))
                continue;
            if (!values.TryGetValue(f.Name, out var raw))
                raw = DefaultFor(f.Name, f.Type, ver);

            switch (f.Type)
            {
                case "B": mainW.WriteB(AsBool(raw)); break;
                case "BS" or "BSd": mainW.WriteBS(AsShort(raw)); break;
                case "BL" or "BLx" or "BLd": mainW.WriteBL(AsInt(raw)); break;
                case "BD": mainW.WriteBD(AsDouble(raw)); break;
                case "RC": mainW.WriteRC(AsByte(raw)); break;
                case "TV": mainW.WriteTV(AsString(raw), false); break;
                case "T": WriteTuNoNul(strW, AsString(raw)); break;
                case "TIMEBLL":
                    var tb = (HvTime)raw!; mainW.WriteBL((int)tb.A); mainW.WriteBL((int)tb.B); break;
                case "BLL": WriteBll(mainW, AsLong(raw)); break;
                case "CMC":
                    var cmc = (HvCmc)raw!;
                    if (!r2004)
                    {
                        int mth = (unchecked((int)cmc.Rgb) >> 24) & 0xFF;
                        mainW.WriteBS((short)(mth == 0xC0 ? 256 : mth == 0xC1 ? 0 : cmc.Index));
                        break;
                    }
                    mainW.WriteBS((short)cmc.Index);
                    mainW.WriteBL((int)cmc.Rgb);
                    mainW.WriteRC((byte)cmc.Flag);
                    if (cmc.Flag < 4)
                    {
                    var s = r7 ? strW : mainW;
                    if ((cmc.Flag & 1) != 0) WriteTuNoNul(s, cmc.Name);
                    if ((cmc.Flag & 2) != 0) WriteTuNoNul(s, cmc.Book);
                    }
                    break;
                case "DH":
                    var dh = (HvHandle)raw!; mainW.WriteHandleRef(dh.Code, dh.Value); break;
                case "H":
                    if (!r7) { var hh = (HvHandle)raw!; mainW.WriteHandleRef(hh.Code, hh.Value); }
                    break;
                case "3BD":
                    var bd3 = (Hv3Bd)raw!; mainW.WriteBD(bd3.X); mainW.WriteBD(bd3.Y); mainW.WriteBD(bd3.Z); break;
                case "2RD":
                    var rd2 = (Hv2Rd)raw!; mainW.WriteRD(rd2.X); mainW.WriteRD(rd2.Y); break;
            }
        }
        if (!r7)
            WriteTrailer(mainW, ver);
    }

    private static void EncodeHandle(BitWriter hdlW, Dictionary<string, object?> values, string ver, bool measureOnly)
    {
        if (!IsR7(ver)) return;
        foreach (var f in HvFieldTable.Fields)
        {
            if (!Active(f.Conds, ver)) continue;
            if (f.Name == "CPSNID" && !CpsnGate(values)) continue;
            if (f.Type != "H") continue;
            if (!values.TryGetValue(f.Name, out var raw))
                raw = new HvHandle(5, 0);
            var h = (HvHandle)raw!;
            hdlW.WriteHandleRef(h.Code, h.Value);
        }
    }

    private static void WriteTrailer(BitWriter w, string ver)
    {
        var r7 = IsR7(ver);
        if (r7)
        {
            for (int i = 0; i < 4; i++) w.WriteBS(unchecked((short)0xFFFF));
        }
        w.WriteBS(0); w.WriteBS(0); w.WriteB(false);
    }

    private static void WriteTuNoNul(BitWriter w, string? s)
    {
        if (string.IsNullOrEmpty(s)) { w.WriteBS(0); return; }
        w.WriteBS(unchecked((short)s!.Length));
        foreach (char c in s) w.WriteRS(c);
    }

    private static object? DefaultFor(string name, string type, string ver)
    {
        if (IsR2004(ver))
        {
            switch (name)
            {
                case "OBSCOLOR":
                case "INTERSECTIONCOLOR": return (short)257;
                case "SORTENTS": return (byte)127;
                case "HIDETEXT": return (byte)1;
                case "DIMASSOC": return (byte)2;
            }
        }
        return type switch
        {
            "B" => false,
            "BS" or "BSd" => name is "unknown_54" or "unknown_55" or "unknown_56" or "unknown_57"
                ? unchecked((short)0xFFFF) : (short)0,
            "BL" or "BLx" or "BLd" => 0,
            "BD" => 0.0,
            "RC" => (byte)0,
            "TV" or "T" => "",
            "TIMEBLL" => new HvTime(0, 0),
            "BLL" => 0L,
            "CMC" => new HvCmc(0, 0, 0, null, null),
            "DH" or "H" => new HvHandle(0, 0),
            "3BD" => new Hv3Bd(0, 0, 0),
            "2RD" => new Hv2Rd(0, 0),
            _ => null
        };
    }

    private static HvHandle ReadHandle(BitReader r)
    {
        byte cc = r.ReadRC();
        int code = cc >> 4, counter = cc & 0x0F;
        ulong value = 0;
        for (int i = counter - 1; i >= 0; i--)
            value |= (ulong)r.ReadRC() << (8 * i);
        return new HvHandle(code, value);
    }

    private static long ReadBll(BitReader r)
    {
        int c = (int)r.ReadBits(3);
        return c switch
        {
            0 => 0,
            1 => r.ReadRC(),
            2 => r.ReadRS(),
            4 => r.ReadRL(),
            _ => throw new InvalidDataException($"BLL 码 {c}")
        };
    }
    private static void WriteBll(BitWriter w, long v)
    {
        if (v == 0) { w.WriteBits(0, 3); }
        else if (v is >= 0 and <= 255) { w.WriteBits(1, 3); w.WriteRC((byte)v); }
        else if (v is >= 0 and <= 65535) { w.WriteBits(2, 3); w.WriteRS((ushort)v); }
        else { w.WriteBits(4, 3); w.WriteRL((uint)v); }
    }

    private static HvCmc ReadCmc(BitReader main, BitReader str, bool r7, bool r2004)
    {
        int index = main.ReadBS();
        if (!r2004)
            return index switch
            {
                256 => new HvCmc(0, unchecked((int)0xC0000000u), 0, null, null),
                0 => new HvCmc(0, unchecked((int)0xC1000000u), 0, null, null),
                _ => new HvCmc(index, unchecked((int)(0xC2000000u | ((uint)index & 0xFFFFFF))), 0, null, null),
            };
        long rgb = main.ReadBL();
        int flag = main.ReadRC();
        string? name = null, book = null;
        if (flag < 4)
        {
            var s = r7 ? str : main;
            if ((flag & 1) != 0) name = s.ReadTV();
            if ((flag & 2) != 0) book = s.ReadTV();
        }
        return new HvCmc(index, rgb, flag, name, book);
    }

    private static HvTime ReadTimeBll(BitReader r) => new(r.ReadBL(), r.ReadBL());
    private static Hv3Bd Read3Bd(BitReader r) => new(r.ReadBD(), r.ReadBD(), r.ReadBD());
    private static Hv2Rd Read2Rd(BitReader r) => new(r.ReadRD(), r.ReadRD());

    private static bool AsBool(object? v) => v is bool b && b;
    private static short AsShort(object? v) => v switch { short s => s, int i => (short)i, _ => 0 };
    private static int AsInt(object? v) => v switch { int i => i, short s => s, long l => (int)l, _ => 0 };
    private static long AsLong(object? v) => v switch { long l => l, int i => i, _ => 0 };
    private static double AsDouble(object? v) => v is double d ? d : 0.0;
    private static byte AsByte(object? v) => v switch { byte b => b, int i => (byte)i, _ => 0 };
    private static string AsString(object? v) => v as string ?? "";
}
