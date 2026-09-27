using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesSourceFrameCrc(DwgObjectRecord rec, byte[] src, Target t, bool reencode)
        => !reencode && !rec.DataRegionRewritten && rec.SourceVersion == t.Version
            && rec.FrameEnd >= 2 && rec.FrameEnd <= src.Length;

    private static void WriteFrameCrc(BitWriter main, int msStart, DwgObjectRecord rec, byte[] src, Target t, bool reencode)
    {
        if (CarriesSourceFrameCrc(rec, src, t, reencode))
        {
            main.AlignByte();
            main.WriteRC(src[rec.FrameEnd - 2]);
            main.WriteRC(src[rec.FrameEnd - 1]);
            return;
        }
        main.WriteObjectCRC(msStart);
    }


    private static bool R2000TargetNoLinks(DwgObjectRecord rec)
        => rec.SourceVersion.IsR2000() ? rec.NoLinks : false;

    private static bool EffXdicMissing(DwgObjectRecord rec)
        => rec.SourceVersion.IsR2004Plus()
            ? (rec.IsXdicMissing || !rec.HasXdicSlot)
            : !(rec.HasXdicSlot && rec.XdicRef.Value != 0);

    private static DwgHandleReference? AxisSlotRef(DwgObjectRecord rec, Target t)
    {
        if (t.Version != DwgVersion.AC1018 || !rec.SourceVersion.IsR2007Plus() || rec.LtypeFlags == 3)
            return null;
        if (rec.ShadowFlags == 3 && rec.HasShadowSlot && rec.ShadowRef.Value != 0) return rec.ShadowRef;
        if (rec.MaterialFlags == 3 && rec.HasMaterialSlot && rec.MaterialRef.Value != 0) return rec.MaterialRef;
        return null;
    }

    private static int AxisSlotFlags(DwgObjectRecord rec, Target t)
        => AxisSlotRef(rec, t) is null ? rec.LtypeFlags : 3;

    private static bool EffHasDsData(DwgObjectRecord rec)
        => rec.SourceVersion >= DwgVersion.AC1027 && rec.HasDsData;

    private static void WritePreview(BitWriter w, DwgObjectRecord rec, byte[] src, Target t)
    {
        w.WriteB(rec.HasPreview);
        if (!rec.HasPreview) return;
        if (t.R2010Plus) w.WriteBLL(rec.PreviewSize);
        else w.WriteRL((uint)rec.PreviewSize);
        if (rec.PreviewPayloadBitLen > 0)
            CopyBits(w, src, rec.PreviewPayloadStartBit, rec.PreviewPayloadBitLen);
    }

    private static void WriteCommonEntityData(BitWriter w, DwgObjectRecord rec, byte[] src, Target t)
    {
        if (t.R2000)
        {
            WritePreview(w, rec, src, t);
            w.WriteBB(rec.EntMode);
            w.WriteBL((int)rec.NumReactors);
            w.WriteB(R2000TargetNoLinks(rec));
            w.WriteBS(rec.ColorIndex);
            w.WriteBD(rec.LinetypeScale);
            w.WriteBB(rec.LtypeFlags);
            w.WriteBB(rec.PlotStyleFlags);
            w.WriteBS(rec.Invisible);
            w.WriteRC(rec.LineWeight);
            return;
        }

        WritePreview(w, rec, src, t);
        w.WriteBB(rec.EntMode);
        w.WriteBL((int)rec.NumReactors);
        w.WriteB(EffXdicMissing(rec));
        if (t.R2013Plus) w.WriteB(EffHasDsData(rec));
        if (rec.ColorBitLen > 0 && !rec.SourceVersion.IsR2000())
            WriteEncColor(w, rec, src, t);
        else
            w.WriteBS(rec.ColorIndex);
        w.WriteBD(rec.LinetypeScale);
        w.WriteBB(AxisSlotFlags(rec, t));
        w.WriteBB(rec.PlotStyleFlags);
        if (t.R2007Plus)
        {
            w.WriteBB(rec.MaterialFlags);
            w.WriteRC(rec.ShadowFlags);
        }
        if (t.R2010Plus)
        {
            w.WriteB(rec.HasFullVisualStyle);
            w.WriteB(rec.HasFaceVisualStyle);
            w.WriteB(rec.HasEdgeVisualStyle);
        }
        w.WriteBS(rec.Invisible);
        w.WriteRC(rec.LineWeight);
        if (NeedsDimensionClassVersion(rec, t))
            w.WriteRC(0);
    }

    private static void WriteEncColor(BitWriter w, DwgObjectRecord rec, byte[] src, Target t)
    {
        if (rec.ColorBitLen <= 0 || rec.ColorStartBit < 0)
        {
            w.WriteBS(rec.ColorIndex);
            return;
        }
        var br = new BitReader(src) { UnicodeStrings = rec.SourceVersion.IsR2007Plus() };
        br.SetPosition((int)(rec.ColorStartBit >> 3), (int)(rec.ColorStartBit & 7));
        int raw = br.ReadBS();
        int flags = (raw >> 8) & 0xFF;
        w.WriteBS((short)raw);
        if ((flags & 0x20) != 0) w.WriteBL(br.ReadBL());
        if ((flags & 0x40) != 0)
        {
            var h = br.ReadHandle();
            w.WriteHandleRef(h.Code, h.Value);
        }
        else if ((flags & 0x80) != 0) w.WriteBL(br.ReadBL());
        if ((flags & 0x41) == 0x41)
        {
            string name = rec.SourceVersion.IsR2007Plus()
                ? rec.EncColor?.ColorName ?? string.Empty
                : br.ReadTV();
            if (!t.UnicodeStrings) w.WriteTV(name, false);
        }
        if ((flags & 0x42) == 0x42)
        {
            string book = rec.SourceVersion.IsR2007Plus()
                ? rec.EncColor?.ColorBookName ?? string.Empty
                : br.ReadTV();
            if (!t.UnicodeStrings) w.WriteTV(book, false);
        }
    }

    private static void WriteEntityHandleStream(BitWriter w, DwgObjectRecord rec, Target t)
    {
        if (rec.EntMode == 0)
            WriteCapturedHandle(w, rec.OwnerRef, rec.OwnerHandle);
        foreach (var rh in rec.ReactorHandles)
            w.WriteHandleRef(rh.Code, rh.Value);
        bool xdicMissing = EffXdicMissing(rec);
        if (t.R2000)
        {
            if (rec.HasXdicSlot && rec.XdicRef.Code != 0)
                w.WriteHandleRef(rec.XdicRef.Code, rec.XdicRef.Value);
            else
                w.WriteRC(0x30);
        }
        else if (!xdicMissing)
            WriteCapturedHandle(w, rec.HasXdicSlot ? rec.XdicRef : default, 0);
        if (t.R2000 && !R2000TargetNoLinks(rec))
        {
            if (rec.HasLinks)
            {
                w.WriteHandleRef(rec.PrevRef.Code, rec.PrevRef.Value);
                w.WriteHandleRef(rec.NextRef.Code, rec.NextRef.Value);
            }
            else
            {
                w.WriteRC(0x40);
                w.WriteRC(0x40);
            }
        }
        WriteCapturedHandle(w, rec.LayerRef, rec.LayerHandle);
        if (rec.LtypeFlags == 3)
            WriteCapturedHandle(w, rec.HasLtypeSlot ? rec.LtypeRef : default, 0);        else if (AxisSlotRef(rec, t) is { } axis)
            w.WriteHandleRef(axis.Code, axis.Value);
        if (t.R2007Plus)
        {
            if (rec.MaterialFlags == 3)
                WriteCapturedHandle(w, rec.HasMaterialSlot ? rec.MaterialRef : default, 0);
            if (rec.ShadowFlags == 3)
                WriteCapturedHandle(w, rec.HasShadowSlot ? rec.ShadowRef : default, 0);
        }
        if (rec.PlotStyleFlags == 3)
            WriteCapturedHandle(w, rec.HasPlotStyleSlot ? rec.PlotStyleRef : default, 0);
        if (t.R2010Plus)
        {
            if (rec.HasFullVisualStyle) w.WriteHandleRef(rec.FullVsRef.Code, rec.FullVsRef.Value);
            if (rec.HasFaceVisualStyle) w.WriteHandleRef(rec.FaceVsRef.Code, rec.FaceVsRef.Value);
            if (rec.HasEdgeVisualStyle) w.WriteHandleRef(rec.EdgeVsRef.Code, rec.EdgeVsRef.Value);
        }
        if (rec.LogicalType == TypeTable && CarriesTableR2010Up(rec, t))
        {
            WriteTableTypeAxisHandlesR2010Up(w, rec);
        }
        else if (rec.LogicalType == TypeTable && CarriesTableR2010Down(rec, t))
        {
            WriteTableTypeAxisHandlesR2010Down(w, rec);
        }
        else if (rec.LogicalType is TypeInsert or TypeMInsert)
        {
            WriteCapturedHandle(w, rec.BlockHeaderRef, rec.BlockHeaderHandle);
            if (rec.HasAttribs)
            {
                if (t.R2000)
                {
                    if (rec.HasAttribPair)
                    {
                        w.WriteHandleRef(rec.FirstAttribRef.Code, rec.FirstAttribRef.Value);
                        w.WriteHandleRef(rec.LastAttribRef.Code, rec.LastAttribRef.Value);
                    }
                    else if (rec.AttribRefs.Count > 0)
                    {
                        w.WriteHandleRef(rec.AttribRefs[0].Code, rec.AttribRefs[0].Value);
                        var last = rec.AttribRefs[^1];
                        w.WriteHandleRef(last.Code, last.Value);
                    }
                }
                else
                {
                    if (rec.AttribRefs.Count > 0)
                    {
                        foreach (var ah in rec.AttribRefs)
                            w.WriteHandleRef(ah.Code, ah.Value);
                    }
                    else if (rec.HasAttribPair)
                    {
                        w.WriteHandleRef(rec.FirstAttribRef.Code, rec.FirstAttribRef.Value);
                        w.WriteHandleRef(rec.LastAttribRef.Code, rec.LastAttribRef.Value);
                    }
                }
                if (rec.HasSeqEndSlot)
                    w.WriteHandleRef(rec.SeqEndRef.Code, rec.SeqEndRef.Value);
            }
        }
        else if (rec.LogicalType is >= TypeDimOrdinate and <= TypeDimDiameter)
        {
            WriteCapturedHandle(w, rec.DimStyleRef, 0);
            WriteCapturedHandle(w, rec.DimBlockRef, 0);
        }
        else if (rec.LogicalType is TypeText or TypeAttrib or TypeAttDef or TypeMText
                 or TypeMline)
        {
            WriteCapturedHandle(w, rec.HasStyleSlot ? rec.StyleRef : default, 0);
            if (rec.LogicalType == TypeMText && NeedsMTextAnnotativeBlock(rec, t))
            {
                w.WriteHandleRef(5, 0);
            }
        }
        else if (rec.LogicalType == TypeTolerance)
        {
            WriteCapturedHandle(w, rec.DimStyleRef, 0);
        }
        else if (rec.LogicalType == TypeHatch)
        {
            foreach (var bh in rec.HatchBoundaryRefs)
                w.WriteHandleRef(bh.Code, bh.Value);
        }
        else if (rec.LogicalType == TypeViewport)
        {
            if (rec.HasVportHeaderSlot && t.R2000)
                w.WriteHandleRef(rec.VportHeaderRef.Code, rec.VportHeaderRef.Value);
            int frozenCode = t.R2000 ? 5 : 4;
            foreach (var f in rec.VportFrozenLayerRefs)
                w.WriteHandleRef(f.Code != 0 ? f.Code : frozenCode, f.Value);
            w.WriteHandleRef(rec.VportClipRef.Code, rec.VportClipRef.Value);
            if (t.R2000 && !rec.HasVportHeaderSlot)
                w.WriteHandleRef(5, 0);
            w.WriteHandleRef(rec.VportNamedUcsRef.Code, rec.VportNamedUcsRef.Value);
            w.WriteHandleRef(rec.VportBaseUcsRef.Code, rec.VportBaseUcsRef.Value);
            if (rec.HasVportR2007Tail && t.R2007Plus)
            {
                w.WriteHandleRef(rec.VportBackgroundRef.Code, rec.VportBackgroundRef.Value);
                w.WriteHandleRef(rec.VportVisualStyleRef.Code, rec.VportVisualStyleRef.Value);
                w.WriteHandleRef(rec.VportShadePlotRef.Code, rec.VportShadePlotRef.Value);
                w.WriteHandleRef(rec.VportSunRef.Code, rec.VportSunRef.Value);
            }
            else if (rec.VportR2007aTailSlot && rec.VportVisualStyleRef.Value != 0)
            {
                w.WriteHandleRef(4, 0);
                w.WriteHandleRef(5, rec.VportVisualStyleRef.Value);
                w.WriteHandleRef(4, 0);
                w.WriteHandleRef(3, 0);
            }
        }
        else if (rec.LogicalType == TypeMLeader)
        {
            for (int i = 0; i < rec.MLeaderUpAddTypeAxisRefs; i++) w.WriteHandleRef(3, 0);
            int drop = rec.SourceVersion.IsR2010Plus()
                       && (t.Version == DwgVersion.AC1021 || t.Version == DwgVersion.AC1018)
                ? rec.MLeaderDownDropTypeAxisRefs : 0;
            for (int i = drop; i < rec.MLeaderTypeAxisRefs.Count; i++)
            {
                var r = rec.MLeaderTypeAxisRefs[i];
                w.WriteHandleRef(r.Code, r.Value);
            }
        }
    }

    private static void WriteCapturedHandle(BitWriter w, DwgHandleReference captured, ulong resolvedFallback)
    {
        if (captured.Code != 0 || captured.Value != 0)
            w.WriteHandleRef(captured.Code, captured.Value);
        else
            w.WriteHandle(resolvedFallback);
    }


    internal sealed class LtypeTable
    {
        public readonly List<ulong> Entries = new();
        public ulong ByBlock;
        public ulong ByLayer;
    }

    private static LtypeTable? CaptureLtypeEntries(DwgObjectReader.WalkResult walk, byte[] src)
    {
        if (src.Length == 0) return null;
        var ctl = walk.Records.FirstOrDefault(
            r => r.LogicalType == TypeLtypeControl && r.CarryStartBit > 0);
        if (ctl == null) return null;
        long hsStart = ctl.HasHandleStream && ctl.HandleStreamStartBit > 0
            ? ctl.HandleStreamStartBit
            : PreR2010HandleStreamStart(src, ctl);
        if (hsStart <= 0) return null;
        return TryCaptureLtypeEntriesFromDataRegion(ctl, src, hsStart)
            ?? TryCaptureLtypeEntriesFromHandleStream(ctl, src, hsStart);
    }

    private static LtypeTable? TryCaptureLtypeEntriesFromDataRegion(
        DwgObjectRecord ctl, byte[] src, long hsStart)
    {
        var table = new LtypeTable();
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, ctl.CarryStartBit);
            if (PrefixHasRl(ctl.SourceVersion))
                br.ReadRL();
            br.ReadHandle();
            if (!SkipEedWriter(br)) return null;
            long numReactors = br.ReadBL();
            bool xdicMissing = br.ReadB();
            if (ctl.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            long numEntries = br.ReadBL();
            if (numReactors is < 0 or > 100000 || numEntries is < 0 or > 100000)
                return null;
            DwgObjectRecord.SeekTo(br, hsStart);
            br.ReadHandle();
            for (long i = 0; i < numReactors; i++) br.ReadHandle();
            if (!xdicMissing) br.ReadHandle();
            for (long i = 0; i < numEntries; i++)
                table.Entries.Add(br.ReadHandle().Resolve(ctl.Handle));
            table.ByBlock = br.ReadHandle().Resolve(ctl.Handle);
            table.ByLayer = br.ReadHandle().Resolve(ctl.Handle);
            if (table.ByLayer == 0) return null;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LTYPE_TABLE(DR) ctl h={ctl.Handle:X} nr={numReactors} xm={xdicMissing} ne={numEntries} " +
                    $"entries=[{string.Join(",", table.Entries.Select(h => h.ToString("X")))}] " +
                    $"byblock={table.ByBlock:X} bylayer={table.ByLayer:X}");
            return table;
        }
        catch { return null; }
    }

    private static LtypeTable? TryCaptureLtypeEntriesFromHandleStream(
        DwgObjectRecord ctl, byte[] src, long hsStart)
    {
        var table = new LtypeTable();
        try
        {
            long frameEnd = (long)(ctl.BodyEnd > 0 ? ctl.BodyEnd : ctl.FrameEnd) * 8;
            if (frameEnd <= hsStart) return null;
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, hsStart);
            var refs = new List<(int Code, ulong Value)>();
            while (br.BitPosition + 8 <= frameEnd)
            {
                var h = br.ReadHandle();
                refs.Add((h.Code, h.Resolve(ctl.Handle)));
            }
            if (refs.Count < 2) return null;
            var byBlockRef = refs[^2];
            var byLayerRef = refs[^1];
            if (byBlockRef.Code != 3 || byLayerRef.Code != 3 || byLayerRef.Value == 0) return null;
            table.ByBlock = byBlockRef.Value;
            table.ByLayer = byLayerRef.Value;
            long numReactors = ReadNumReactors(ctl, src);
            int prefix = 1 + (int)numReactors;
            int entriesFrom = refs.Count > prefix && refs[prefix].Code is not 2 ? prefix + 1 : prefix;
            for (int i = entriesFrom; i < refs.Count - 2; i++)
                if (refs[i].Code == 2 && refs[i].Value != 0)
                    table.Entries.Add(refs[i].Value);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LTYPE_TABLE(HS) ctl h={ctl.Handle:X} nr={numReactors} refs={refs.Count} " +
                    $"entries=[{string.Join(",", table.Entries.Select(h => h.ToString("X")))}] " +
                    $"byblock={table.ByBlock:X} bylayer={table.ByLayer:X}");
            return table;
        }
        catch { return null; }
    }

    private static long ReadNumReactors(DwgObjectRecord ctl, byte[] src)
    {
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, ctl.CarryStartBit);
            if (PrefixHasRl(ctl.SourceVersion)) br.ReadRL();
            br.ReadHandle();
            if (!SkipEedWriter(br)) return 0;
            long nr = br.ReadBL();
            return nr is >= 0 and <= 100000 ? nr : 0;
        }
        catch { return 0; }
    }

    internal static long PreR2010HandleStreamStart(byte[] src, DwgObjectRecord ctl)
    {
        if (ctl.SourceVersion >= DwgVersion.AC1024 || ctl.BodyStart <= 0 || ctl.CarryStartBit <= 0)
            return 0;
        long rl = ReadRlAt(src, ctl.CarryStartBit);
        if (rl <= 0 || rl > (long)src.Length * 8)
            return 0;
        long hs = (long)ctl.BodyStart * 8 + rl;
        long frameEnd = (long)(ctl.BodyEnd > 0 ? ctl.BodyEnd : ctl.FrameEnd) * 8;
        if (hs <= ctl.CarryStartBit || hs >= frameEnd)
            return 0;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LTYPE_HS_DERIVED ctl h={ctl.Handle:X} v={ctl.SourceVersion} rl={rl} hs={hs} frameEnd={frameEnd}");
        return hs;
    }

    internal static bool SkipEedWriter(BitReader br)
    {
        try
        {
            while (true)
            {
                int len = br.ReadBS();
                if (len == 0) return true;
                br.ReadHandle();
                for (int i = 0; i < len; i++) br.ReadRC();
            }
        }
        catch { return false; }
    }

    internal static void SkipCmcMainstream(BitReader br)
    {
        br.ReadBS();
        br.ReadBL();
        br.ReadRC();
    }

    internal static ulong LtypeHandleForIndex(int index, LtypeTable table)
        => index switch
        {
            32767 => table.ByLayer,
            32766 => table.ByBlock,
            0 => table.Entries.Count > 0 ? table.Entries[0] : 0,
            > 0 => index < table.Entries.Count ? table.Entries[index] : 0,
            _ => 0,
        };

    internal static int LtypeIndexForHandle(ulong handle, LtypeTable table)
    {
        if (handle == table.ByLayer) return 32767;
        if (handle == table.ByBlock) return 32766;
        for (int i = 0; i < table.Entries.Count; i++)
            if (table.Entries[i] == handle) return i;
        return 0;
    }

    internal static void CopyCommonHead(BitWriter body, byte[] src, long start, long end,
        long dsStart, long dsEnd, long afterXdic, Target t)
    {
        bool srcHasDs = dsStart >= 0;
        if (end <= start || srcHasDs == t.R2013Plus
            || (srcHasDs && dsEnd <= dsStart))
        {
            CopyBits(body, src, start, (int)(end - start));
            return;
        }
        if (srcHasDs)
        {
            CopyBits(body, src, start, (int)(dsStart - start));
            CopyBits(body, src, dsEnd, (int)(end - dsEnd));
        }
        else
        {
            CopyBits(body, src, start, (int)(afterXdic - start));
            body.WriteB(false);
            CopyBits(body, src, afterXdic, (int)(end - afterXdic));
        }
    }
}
