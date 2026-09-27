using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    private static void WalkEntityHandles(byte[] data, DwgEntity e, short type, uint bitsize, int objStart,
        ICommonEntityData ced, bool hasAttribs, DwgVersion version, DwgObjectRecord? record)
    {
        bool r2000 = version.IsR2000();
        bool r2010Plus = version.IsR2010Plus();
        try
        {
            long bitPos = (long)objStart * 8 + bitsize;
            int hdlByte = objStart + (int)Math.Min(bitsize / 8, 1 << 28);
            if (hdlByte <= 0 || hdlByte >= data.Length)
                return;
            var hdl = new BitReader(data, hdlByte);
            for (int i = 0; i < bitsize % 8 && !hdl.EndOfStream; i++)
                hdl.ReadB();

            EntityFactory.CreateEntityHandleStream(version).Read(hdl, e, record, ced);

            if (type is TypeInsert or TypeMInsert)
            {
                var blockHeader = hdl.ReadHandle();
                e.BlockHeaderHandle = blockHeader.Resolve(e.Handle);
                if (record != null)
                {
                    record.BlockHeaderRef = blockHeader;
                    if (hasAttribs)
                    {
                        if (r2000)
                        {
                            record.HasAttribPair = true;
                            record.FirstAttribRef = hdl.ReadHandle();
                            record.LastAttribRef = hdl.ReadHandle();
                        }
                        else
                        {
                            for (uint i = 0; i < record.NumOwned; i++)
                                record.AttribRefs.Add(hdl.ReadHandle());
                        }
                        record.HasSeqEndSlot = true;
                        record.SeqEndRef = hdl.ReadHandle();
                    }
                }
            }
            else if (type == TypeTable && record?.Table is { Captured: true } tblCap
                     && e is not TableAcR2010Plus)
            {
                long axisStart = hdl.BitPosition;
                long hsEnd = (long)record.FrameEnd * 8;
                if (record.StringStreamStartBit > axisStart && record.StringStreamStartBit < hsEnd)
                    hsEnd = record.StringStreamStartBit;
                for (int i = 0; i < 4096; i++)
                {
                    long peek = hdl.BitPosition;
                    if (peek + 8 > hsEnd) break;
                    byte rc = hdl.ReadRC();
                    int bits = 8 + (rc & 0x0F) * 8;
                    if (peek + bits > hsEnd) break;
                    ulong val = 0;
                    for (int k = 0; k < (rc & 0x0F); k++) val = (val << 8) | hdl.ReadRC();
                    var tr = new DwgHandleReference(rc >> 4, val);
                    if (tr.Code == 0 && tr.Value == 0) break;
                    tblCap.TypeAxisRefs.Add(tr);
                }
                hdl.SetPosition((int)(axisStart >> 3), (int)(axisStart & 7));

                if (e is TableEntity tblEnt && tblEnt.Cells.Count > 0)
                {
                    int f10 = 0;
                    foreach (var cl0 in tblEnt.Cells)
                        if (cl0.HasOverride && (cl0.CellFlagOverride & 0x10) != 0) f10++;
                    var axis = tblCap.TypeAxisRefs;
                    if (axis.Count == 2 + tblEnt.Cells.Count + f10)
                    {
                        int p = 2;
                        foreach (var cl in tblEnt.Cells)
                        {
                            p++;
                            if (cl.HasOverride && (cl.CellFlagOverride & 0x10) != 0)
                                cl.TextStyleHandle = axis[p++].Resolve(tblEnt.Handle);
                        }
                    }
                }
            }
            else if (type == TypeTable && e is TableAcR2010Plus thickTbl && record != null
                     && thickTbl.NumFieldRefs >= 0)
            {
                long axisStart = hdl.BitPosition;
                long hsEnd = (long)record.FrameEnd * 8;
                var axis = new List<DwgHandleReference>();
                bool axisClosed = false;
                for (int i = 0; i < 8192; i++)
                {
                    long peek = hdl.BitPosition;
                    if (peek + 8 > hsEnd) break;
                    byte rc = hdl.ReadRC();
                    int bits = 8 + (rc & 0x0F) * 8;
                    if (peek + bits > hsEnd) { axisClosed = true; break; }
                    ulong val = 0;
                    for (int k = 0; k < (rc & 0xF); k++) val = (val << 8) | hdl.ReadRC();
                    if ((rc >> 4) == 0) { axisClosed = true; break; }
                    axis.Add(new DwgHandleReference(rc >> 4, val));
                    if (hsEnd - hdl.BitPosition < 8) { axisClosed = true; break; }
                }
                hdl.SetPosition((int)(axisStart >> 3), (int)(axisStart & 7));
                if (record.Table is { } thickCap)
                {
                    thickCap.TypeAxisRefs.Clear();
                    thickCap.TypeAxisRefs.AddRange(axis);
                }
                if (axisClosed) AttributeThickTableHandles(thickTbl, axis);
            }
            else if (type is >= TypeDimOrdinate and <= TypeDimDiameter)
            {
                var dimstyle = hdl.ReadHandle();
                if (record != null) record.DimStyleRef = dimstyle;
                e.DimStyleHandle = dimstyle.Resolve(e.Handle);
                var dimBlk = hdl.ReadHandle();
                e.DimBlockHandle = dimBlk.Resolve(e.Handle);
                if (record != null) record.DimBlockRef = dimBlk;
            }
            else if (type is TypeText or TypeMText or TypeAttrib or TypeAttDef)
            {
                var style = hdl.ReadHandle();
                e.TextStyleHandle = style.Resolve(e.Handle);
                if (record != null) { record.HasStyleSlot = true; record.StyleRef = style; }
            }
            else if (type == TypeLeader && record != null)
            {
                long save = hdl.BitPosition;
                long hsEnd = (long)record.BodyEnd * 8;
                try
                {
                    long meaningful = DwgObjectStreamWriter.LastMeaningfulHandleEnd(hdl.UnderlyingData(), record.HandleStreamStartBit > 0 ? record.HandleStreamStartBit : save, hsEnd);
                    DwgHandleReference last = default;
                    bool ok = false, sawAny = false;
                    for (int i = 0; i < 3 && hdl.BitPosition + 8 <= hsEnd; i++)
                    {
                        long p = hdl.BitPosition;
                        byte rc = hdl.ReadRC();
                        int bits = 8 + (rc & 0x0F) * 8;
                        if (p + bits > hsEnd) break;
                        ulong val = 0;
                        for (int k = 0; k < (rc & 0xF); k++) val = (val << 8) | hdl.ReadRC();
                        if ((rc >> 4) != 5) break;
                        last = new DwgHandleReference(5, val);
                        sawAny = true;
                        ok = hdl.BitPosition == meaningful;
                    }
                    if (sawAny && ok)
                    {
                        record.LeaderDimStyleRef = last;
                        e.DimStyleHandle = last.Resolve(e.Handle);
                        record.HasLeaderDimStyleSlot = true;
                    }
                }
                finally { DwgObjectRecord.SeekTo(hdl, save); }
            }
            else if (type is TypeTolerance or TypeMline)
            {
                var st = hdl.ReadHandle();
                if (type == TypeTolerance)
                {
                    e.DimStyleHandle = st.Resolve(e.Handle);
                    if (record != null) record.DimStyleRef = st;
                }
                else
                {
                    if (e is MLineEntity ml) ml.MLineStyleHandle = st.Resolve(e.Handle);
                    if (record != null) { record.HasStyleSlot = true; record.StyleRef = st; }
                }
            }
            else if (type == TypeMLeader && e is MLeaderEntity ml && ml.Captured)
            {
                if (record == null)
                {
                    if (r2010Plus)
                    {
                        foreach (var node in ml.Leaders)
                            foreach (var line in node.Lines) { hdl.ReadHandle(); hdl.ReadHandle(); }
                    }
                    if (ml.HasTextContent)
                        ml.ContentStyleHandle = hdl.ReadHandle().Resolve(ml.Handle);
                    else if (ml.HasBlockContent)
                        hdl.ReadHandle();
                    ml.MLeaderStyleHandle = hdl.ReadHandle().Resolve(ml.Handle);
                    hdl.ReadHandle(); hdl.ReadHandle();
                    e.TextStyleHandle = hdl.ReadHandle().Resolve(ml.Handle);
                    hdl.ReadHandle();
                    if (!r2010Plus)
                    {
                        for (int i = 0; i < ml.ArrowheadCount; i++) hdl.ReadHandle();
                        for (int i = 0; i < ml.BlockLabelCount; i++) hdl.ReadHandle();
                    }
                }
                else
                {
                    if (r2010Plus)
                    {
                        foreach (var node in ml.Leaders)
                            foreach (var line in node.Lines)
                            {
                                record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                                record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                            }
                    }
                    if (ml.HasTextContent)
                    {
                        var cstyle = hdl.ReadHandle();
                        ml.ContentStyleHandle = cstyle.Resolve(ml.Handle);
                        record.MLeaderTypeAxisRefs.Add(cstyle);
                    }
                    else if (ml.HasBlockContent)
                        record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                    var mlstyle = hdl.ReadHandle();
                    ml.MLeaderStyleHandle = mlstyle.Resolve(ml.Handle);
                    record.MLeaderTypeAxisRefs.Add(mlstyle);
                    record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                    record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                    var tstyle = hdl.ReadHandle();
                    e.TextStyleHandle = tstyle.Resolve(ml.Handle);
                    record.MLeaderTypeAxisRefs.Add(tstyle);
                    record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                    if (!r2010Plus)
                    {
                        for (int i = 0; i < ml.ArrowheadCount; i++)
                            record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                        for (int i = 0; i < ml.BlockLabelCount; i++)
                            record.MLeaderTypeAxisRefs.Add(hdl.ReadHandle());
                    }
                }
            }
            else if (type == TypeHatch && record != null && record.HatchBoundaryHandleTotal > 0)
            {
                for (long i = 0; i < record.HatchBoundaryHandleTotal; i++)
                    record.HatchBoundaryRefs.Add(hdl.ReadHandle());
            }
            else if (type == TypeViewport && record != null && e is ViewportEntity vp)
            {
                if (version < DwgVersion.AC1018)
                {
                    record.HasVportHeaderSlot = true;
                    record.VportHeaderRef = hdl.ReadHandle();
                }
                for (long i = 0; i < vp.NumFrozenLayers; i++)
                    record.VportFrozenLayerRefs.Add(hdl.ReadHandle());
                record.VportClipRef = hdl.ReadHandle();
                record.VportNamedUcsRef = hdl.ReadHandle();
                record.VportBaseUcsRef = hdl.ReadHandle();
                if (version >= DwgVersion.AC1021)
                {
                    record.HasVportR2007Tail = true;
                    record.VportBackgroundRef = hdl.ReadHandle();
                    record.VportVisualStyleRef = hdl.ReadHandle();
                    record.VportShadePlotRef = hdl.ReadHandle();
                    record.VportSunRef = hdl.ReadHandle();
                }
                else
                {
                    record.VportR2007aTailSlot = true;
                }
            }
            if (record != null) record.HandleWalkEndBit = hdl.BitPosition;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException)
        {
        }
    }

    private static void AttributeThickTableHandles(TableAcR2010Plus t, List<DwgHandleReference> axis)
    {
        var pred = new List<(int code, int kind, object? target)>(axis.Count + 8);
        void Style(TableCellStyle? st)
        {
            if (st is not { DataFlags: not 0 }) return;
            pred.Add((5, 1, st.ContentFormat));
            foreach (var b in st.Borders) pred.Add((5, 2, b));
        }
        pred.Add((5, 0, null));
        pred.Add((4, 0, null));
        foreach (var cs in t.ColumnStyles) Style(cs);
        int idx = 0;
        for (int r = 0; r < t.HsRowCellCounts.Count; r++)
        {
            if (r >= t.RowStyles.Count || idx + t.HsRowCellCounts[r] > t.Cells.Count) return;
            for (int k = 0; k < t.HsRowCellCounts[r]; k++, idx++)
            {
                var c = t.Cells[idx];
                if (c.HsHasDataLink) pred.Add((5, 0, null));
                if (c.Type == 2) pred.Add((5, 0, null));
                foreach (var a in c.AttrDefs) pred.Add((5, 3, a));
                if (c.ValueDataType == 0x40) pred.Add((5, 0, null));
                if (c.ContentFormat is { } f0) pred.Add((5, 1, f0));
                Style(c.CellStyle);
                if (c.HsHasGeomData) pred.Add((4, 0, null));
            }
            Style(t.RowStyles[r]);
        }
        if (idx != t.Cells.Count) return;
        for (int i = 0; i < t.NumFieldRefs; i++) pred.Add((5, 0, null));
        Style(t.BaseCellStyle);
        pred.Add((5, 4, null));
        if (pred.Count != axis.Count) return;
        for (int i = 0; i < pred.Count; i++)
            if (pred[i].code != axis[i].Code) return;
        for (int i = 0; i < pred.Count; i++)
        {
            int kind = pred[i].kind;
            if (kind == 0) continue;
            ulong h = axis[i].Resolve(t.Handle);
            switch (kind)
            {
                case 1: if (pred[i].target is TableContentFormat f) f.TextStyleHandle = h; break;
                case 2: if (pred[i].target is TableCellBorder b) b.LineTypeHandle = h; break;
                case 3: if (pred[i].target is TableCellAttrDef a) a.AttdefHandle = h; break;
                case 4: t.TableStyleHandle = h; break;
            }
        }
    }
}
