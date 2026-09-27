using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool IsSynthetic(DwgObjectRecord rec)
        => rec.HasSyntheticFrame && rec.Synthetic is { Kind: not SynthEntityKind.None };

    private static bool WriteSyntheticFrame(BitWriter main, DwgObjectRecord rec, Target t)
    {
        var syn = rec.Synthetic!;
        rec.WroteCentralStringStream = t.R2007Plus && syn.Kind == SynthEntityKind.MText;

        var body = new BitWriter(256);
        WriteSyntheticOwnAndEed(body, rec);
        WriteSyntheticCommonEntityData(body, rec, t);
        switch (syn.Kind)
        {
            case SynthEntityKind.MText:
                WriteSyntheticMTextBody(body, rec, syn.MText!, t);
                break;
            case SynthEntityKind.LwPolyline:
                WriteSyntheticPolylineBody(body, syn.Polyline!);
                break;
            default:
                return false;
        }

        var handles = new BitWriter(64);
        WriteSyntheticHandleStream(handles, rec, t);


        var typeW = new BitWriter(8);
        short code = rec.LogicalType != 0 ? rec.LogicalType : rec.TypeCode;
        if (t.R2010Plus) typeW.WriteBOT(code); else typeW.WriteBS(code);

        int typeBits = (int)typeW.BitPosition;
        return FinalizeObjectFrame(main, body, handles, -1, typeW, typeBits,
            bodyCarriesRl: false, rec, Array.Empty<byte>(), t, reencode: true,
            cfDataEnd: -1, derivedHsBit: -1, handlesSplitFromSource: true);
    }

    private static void WriteSyntheticOwnAndEed(BitWriter w, DwgObjectRecord rec)
    {
        w.WriteHandleRef(0, rec.Handle);
        w.WriteBS(0);
    }

    private static void WriteSyntheticCommonEntityData(BitWriter w, DwgObjectRecord rec, Target t)
    {
        w.WriteB(false);
        w.WriteBB(rec.EntMode);
        w.WriteBL((int)rec.NumReactors);
        w.WriteB(t.R2000 ? rec.NoLinks : true);
        if (t.R2013Plus) w.WriteB(false);
        w.WriteBS(rec.ColorIndex != 0 ? rec.ColorIndex : (short)256);
        w.WriteBD(rec.LinetypeScale);
        w.WriteBB(rec.LtypeFlags);
        w.WriteBB(rec.PlotStyleFlags);
        if (t.R2007Plus)
        {
            w.WriteBB(0);
            w.WriteRC(0);
        }
        if (t.R2010Plus)
        {
            w.WriteB(false); w.WriteB(false); w.WriteB(false);
        }
        w.WriteBS(rec.Invisible);
        w.WriteRC(rec.LineWeight);
    }

    private static void WriteSyntheticTv(BitWriter w, string value, System.Text.Encoding? enc)
    {
        if (enc is null) { w.WriteTV(value, false); return; }
        byte[] bytes = enc.GetBytes(value);
        if (bytes.Length > 0) bytes = TrimTrailingNul(bytes);
        w.WriteBS(unchecked((short)bytes.Length));
        foreach (var b in bytes) w.WriteRC(b);
    }

    private static byte[] TrimTrailingNul(byte[] b)
    {
        int n = b.Length;
        while (n > 0 && b[n - 1] == 0) n--;
        if (n == b.Length) return b;
        var r = new byte[n];
        Array.Copy(b, r, n);
        return r;
    }

    private static void WriteSyntheticMTextBody(BitWriter w, DwgObjectRecord rec,
        SyntheticMText m, Target t)
    {
        w.WriteBD(m.InsX); w.WriteBD(m.InsY); w.WriteBD(m.InsZ);
        w.WriteBD(m.ExtrusionX); w.WriteBD(m.ExtrusionY); w.WriteBD(m.ExtrusionZ);
        w.WriteBD(m.XAxisX); w.WriteBD(m.XAxisY); w.WriteBD(m.XAxisZ);
        w.WriteBD(m.RectWidth);
        if (t.R2007Plus) w.WriteBD(0.0);
        w.WriteBD(m.TextHeight);
        w.WriteBS(m.Attachment);
        w.WriteBS(m.FlowDir);
        w.WriteBD(m.ExtentsHeight);
        w.WriteBD(m.ExtentsWidth);
        if (!t.R2007Plus) WriteSyntheticTv(w, m.Text, m.LegacyEncoding);
        w.WriteBS(m.LineSpaceStyle);
        w.WriteBD(m.LineSpaceFactor);
        w.WriteB(m.LineSpaceUnknownBit);
        if (t.Version >= DwgVersion.AC1018)
        {
            w.WriteB(m.BgFillFlag == 0);
            w.WriteB(false);
        }

        if (t.Version >= DwgVersion.AC1032) w.WriteB(false);
        if (t.R2007Plus)
        {
            long cs = w.BitPosition;
            WriteCentralTu(w, m.Text);
            WriteStringStreamSize(w, (uint)(w.BitPosition - cs));
            w.WriteB(true);

        }
    }

    internal static void WriteSyntheticPolylineBodyForTest(BitWriter w, SyntheticPolyline p)
        => WriteSyntheticPolylineBody(w, p);

    private static void WriteSyntheticPolylineBody(BitWriter w, SyntheticPolyline p)
    {
        short flag = 0;
        if (p.Closed) flag |= 512;
        if (p.PlineGen) flag |= 256;
        if (p.HasElevation) flag |= 8;
        w.WriteBS(flag);
        if (p.HasElevation) w.WriteBD(p.Elevation);
        int n = Math.Min(p.X.Length, p.Y.Length);
        w.WriteBL(n);
        if (n == 0) return;
        w.WriteRD(p.X[0]); w.WriteRD(p.Y[0]);
        for (int i = 1; i < n; i++)
        {
            w.WriteDD(p.X[i], p.X[i - 1]);
            w.WriteDD(p.Y[i], p.Y[i - 1]);
        }
    }

    private static void WriteSyntheticHandleStream(BitWriter w, DwgObjectRecord rec, Target t)
    {
        if (t.R2000)
        {
            if (rec.EntMode == 0) w.WriteHandleRef(4, rec.OwnerHandle);
            w.WriteRC(0x30);
            if (!rec.NoLinks)
            {
                w.WriteHandleRef(rec.PrevRef.Code, rec.PrevRef.Value);
                w.WriteHandleRef(rec.NextRef.Code, rec.NextRef.Value);
            }
        }
        else if (rec.EntMode == 0) w.WriteHandleRef(0, rec.OwnerHandle);
        for (uint i = 0; i < Math.Min(rec.NumReactors, 10000u); i++) w.WriteHandleRef(3, 0);
        w.WriteHandleRef(5, rec.LayerHandle);
        if (rec.Synthetic!.Kind == SynthEntityKind.MText)
            w.WriteHandleRef(5, rec.StyleRef.Value);
    }
}

public static partial class DwgObjectStreamWriter
{
    internal static bool TryRewriteBlockHeaderOwnedHandles(BitWriter handles, byte[] src,
        DwgObjectRecord rec, long hsStart, long dataEnd, int srcOwnedCount)
    {
        var owned = rec.BlockOwnedHandles!;
        var br = new BitReader(src, 0);
        int prevCode = -1;
        bool emitted = false;
        bool blockSeen = false;
        for (long hp = hsStart; hp + 8 <= dataEnd;)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            int n = cc & 0xF, code = cc >> 4;
            int bits = 8 + n * 8;
            if (hp + bits > dataEnd) break;
            if (code == 0 && n == 0) break;
            ulong v = 0;
            for (int i = 0; i < n; i++) v = (v << 8) | br.ReadRC();
            if (!emitted && blockSeen && code == 3 && prevCode == 3)
            {
                foreach (var oh in owned) handles.WriteHandleRef(3, oh);
                emitted = true;
                long q = hp;
                for (int i = 0; i < srcOwnedCount; i++)
                {
                    if (q + 8 > dataEnd) break;
                    DwgObjectRecord.SeekTo(br, q);
                    int c2 = br.ReadRC();
                    int b2 = 8 + (c2 & 0xF) * 8;
                    if (q + b2 > dataEnd) break;
                    q += b2;
                }
                hp = q;
                prevCode = -1;
                continue;
            }
            handles.WriteHandleRef(code, v);
            if (code == 3 && prevCode == 5) blockSeen = true;
            prevCode = code;
            hp += bits;
        }
        if (!emitted)
            System.Diagnostics.Debug.WriteLine($"BHOWN60 emit-miss h={rec.Handle:X}");
        return emitted;
    }
}
