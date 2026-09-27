using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class TableStyleAcR2010Plus : TableStyleRecord
{
    public override string Family => "R2010+";

    public TableStyleAcR2010Plus(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    private sealed class CellStyleWalk
    {
        public bool Thin;
        public double M0, M1, TextHeight;
        public int Alignment, Vdt, Vut;
        public (int Idx, uint Rgb, byte Flag) Bg, Content;
        public readonly int[] Lw = new int[6];
        public readonly (int Idx, uint Rgb, byte Flag)[] Col = new (int, uint, byte)[6];
        public readonly bool[] Vis = new bool[6];
        public int NumBorders;
        public string? Bail;
        public readonly TableStyleCellStyle Style = new();
    }

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion < DwgVersion.AC1024) return false;
        long hs = Rec.HandleStreamStartBit;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return false; }
        var ts = Cap;
        try
        {
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
            int numReactors = br.ReadBL();
            bool xdicMissing = br.ReadB();
            if (SrcVersion >= DwgVersion.AC1027) br.ReadB();
            if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return false; }
            long headEnd = br.BitPosition;
            if (br.ReadRC() != 0) { Fail(br, "表头 hdr RC≠0"); return false; }
            int hdrFlags = br.ReadBL();
            if (hdrFlags is < 0 or > 1024) { Fail(br, $"表头第 2 枚 BL={hdrFlags}"); return false; }
            ts.Flags = hdrFlags;
            ts.HeaderNonDefault = hdrFlags != 0;
            if (br.ReadBL() != 101) { Fail(br, "表头第 3 枚 BL≠101"); return false; }

            var baseW = new CellStyleWalk { Thin = true };
            if (!ReadTableCellStyle(br, baseW)) { Fail(br, $"cellstyle(base):{baseW.Bail}"); return false; }
            br.ReadBL(); br.ReadBL();

            int numOvr = br.ReadBL();
            if (numOvr is < 0 or > 8) { Fail(br, $"numoverrides={numOvr}"); return false; }
            if (numOvr != 3) baseW.Thin = false;
            var byBl3 = new CellStyleWalk?[3];
            var ovrOrder = new List<(int Bl3, CellStyleWalk W)>(numOvr);
            for (int i = 0; i < numOvr; i++)
            {
                int bl3 = br.ReadBL();
                if (bl3 is < 0 or > 8) { Fail(br, $"override[{i}] bl3={bl3}"); return false; }
                var w = new CellStyleWalk { Thin = true };
                if (!ReadTableCellStyle(br, w)) { Fail(br, $"cellstyle(ovr{i}):{w.Bail}"); return false; }
                br.ReadBL(); br.ReadBL();
                ovrOrder.Add((bl3, w));
                if (bl3 is >= 1 and <= 3 && byBl3[bl3 - 1] is null) byBl3[bl3 - 1] = w;
                else w.Thin = false;
                if (!w.Thin) baseW.Thin = false;
            }
            if (byBl3[0] is null || byBl3[1] is null || byBl3[2] is null) baseW.Thin = false;

            if (!CentralStrings.TryDataBits(Rec, ObjStart, br, out uint bitsize, out _))
            { Fail(br, "集中流 bitsize 未定位"); return false; }
            if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bitsize,
                    out BitReader str, out long areaStart, out long areaEnd))
            { Fail(br, "集中流串区量不出"); return false; }
            if (!CentralStrings.ReadAll(str, areaEnd, 64, out var tus))
            { Fail(br, "集中流 TU 逐枚读出越界"); return false; }
            if (tus.Count == 0 || tus[0].Length == 0 || tus[0].Length > 255)
            { Fail(br, $"样式名长度={(tus.Count == 0 ? -1 : tus[0].Length)}"); return false; }
            string name = tus[0];

            ts.ValuesCommitted = true;
            ts.Name = name;
            ts.HorizMargin = baseW.M1;
            ts.VertMargin = baseW.M0;
            ts.BaseCellStyle = baseW.Style;
            foreach (var (b3, ow) in ovrOrder) ts.OverrideCellStyles.Add((b3, ow.Style));
            MapRows(ts, byBl3);
            if (!baseW.Thin)
            {
                br.SetPosition((int)(hs >> 3), (int)(hs & 7));
                br.ReadHandle();
                for (int i = 0; i < numReactors; i++) br.ReadHandle();
                if (!xdicMissing) br.ReadHandle();
                ts.HeadEndBit = headEnd;
                ts.HsCommonEndBit = br.BitPosition;
                ts.DownReady = true;
                try
                {
                    long frameEnd = (long)Rec.BodyEnd * 8;
                    br.ReadHandle();
                    ulong baseTsty = TakeTsty(br, frameEnd);
                    for (int b = 0; b < baseW.NumBorders; b++) br.ReadHandle();
                    var ovrT = new (int Bl3, ulong T)[ovrOrder.Count];
                    for (int i = 0; i < ovrOrder.Count; i++)
                    {
                        ovrT[i] = (ovrOrder[i].Bl3, TakeTsty(br, frameEnd));
                        for (int b = 0; b < ovrOrder[i].W.NumBorders; b++) br.ReadHandle();
                    }
                    ulong T(int bl3)
                    {
                        foreach (var (b3, t) in ovrT)
                            if (b3 == bl3) return t;
                        return baseTsty;
                    }
                    ts.Tsty[0] = T(3);
                    ts.Tsty[1] = T(1);
                    ts.Tsty[2] = T(2);
                }
                catch {  }
                if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                    System.Console.Error.WriteLine(
                        $"TSDOWN-RICH h={Rec.Handle:X} DownReady=1 name='{name}' nr={numReactors} " +
                        $"ovr={ovrOrder.Count} nb=[base={baseW.NumBorders}," +
                        $"{string.Join(",", ovrOrder.Select(o => o.W.NumBorders))}] " +
                        $"hs={hs} commonEnd={ts.HsCommonEndBit} " +
                        $"tsty=[{ts.Tsty[0]:X},{ts.Tsty[1]:X},{ts.Tsty[2]:X}] " +
                        $"cellStyles=[{ts.BaseCellStyle?.DataFlags}" +
                        $"/{string.Join(",", ts.OverrideCellStyles.Select(o => $"{o.Bl3}:{o.Style.DataFlags}"))}]");
                return true;
            }

            br.SetPosition((int)(hs >> 3), (int)(hs & 7));
            br.ReadHandle();
            for (int i = 0; i < numReactors; i++) br.ReadHandle();
            if (!xdicMissing) br.ReadHandle();
            long commonEnd = br.BitPosition;
            br.ReadHandle();
            ulong[] sT = new ulong[4];
            for (int s = 0; s < 4; s++)
            {
                var h = br.ReadHandle();
                if (h.Code != 5) { Fail(br, $"句柄流 tsty[{s}] code={h.Code}≠5"); return false; }
                sT[s] = h.Value;
                for (int b = 0; b < 6; b++)
                    if (br.ReadHandle().Code != 5)
                    { Fail(br, $"句柄流 边框引用[{s}][{b}] code≠5"); return false; }
            }
            ts.Captured = true;
            ts.HeadEndBit = headEnd;
            ts.HsCommonEndBit = commonEnd;
            ts.Tsty[0] = sT[3];
            ts.Tsty[1] = sT[1];
            ts.Tsty[2] = sT[2];
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                System.Console.Error.WriteLine(
                    $"TSDOWN h={Rec.Handle:X} OK name='{name}' horiz={ts.HorizMargin} " +
                    $"vert={ts.VertMargin} rows=[{ts.Rows[0].TextHeight}/{ts.Rows[0].Alignment}," +
                    $"{ts.Rows[1].TextHeight}/{ts.Rows[1].Alignment}," +
                    $"{ts.Rows[2].TextHeight}/{ts.Rows[2].Alignment}] " +
                    $"hs={hs} commonEnd={commonEnd} tsty=[{ts.Tsty[0]:X},{ts.Tsty[1]:X},{ts.Tsty[2]:X}]");
        }
        catch (Exception ex)
        { Fail(br, $"走查异常:{ex.GetType().Name}"); return false; }
        return true;
    }

    private static ulong TakeTsty(BitReader br, long frameEnd)
    {
        if (br.BitPosition + 8 > frameEnd) throw new EndOfStreamException();
        var h = br.ReadHandle();
        if (h.Code != 5) throw new InvalidDataException();
        return h.Value;
    }

    private static bool ReadTableCellStyle(BitReader br, CellStyleWalk w)
    {
        var st = w.Style;
        st.Type = br.ReadBL();
        short dataFlags = st.DataFlags = br.ReadBS();
        if (dataFlags == 0) { w.Thin = false; return true; }
        st.PropertyOverrideFlags = br.ReadBL();
        st.MergeFlags = br.ReadBL();
        w.Bg = DwgObjectReader.ReadCmcTriple(br);
        st.BackgroundColor = ToCmc(w.Bg);
        int contentLayout = st.ContentLayout = br.ReadBL();
        if (contentLayout == 0) w.Thin = false;
        var cf = new TableStyleContentFormat();
        cf.PropertyOverrideFlags = br.ReadBL();
        cf.PropertyFlags = br.ReadBL();
        w.Vdt = cf.ValueDataType = br.ReadBL();
        w.Vut = cf.ValueUnitType = br.ReadBL();
        if (w.Vdt is < 0 or > 1024 || w.Vut is < 0 or > 200)
        { w.Bail = $"cf 值域 vdt={w.Vdt}/vut={w.Vut}"; return false; }
        cf.ValueFormatString = br.ReadTV();
        cf.Rotation = br.ReadBD();
        cf.BlockScale = br.ReadBD();
        w.Alignment = cf.CellAlignment = br.ReadBL();
        w.Content = DwgObjectReader.ReadCmcTriple(br);
        cf.ContentColor = ToCmc(w.Content);
        cf.ValueStyle = br.ReadTV();
        w.TextHeight = cf.TextHeight = br.ReadBD();
        if (double.IsNaN(w.TextHeight) || w.TextHeight < 0 || w.TextHeight > 1e6)
        { w.Bail = $"text_height={w.TextHeight}"; return false; }
        short marginOvr = st.MarginOverrideFlags = br.ReadBS();
        if (marginOvr == 0) w.Thin = false;
        else
        {
            w.M0 = br.ReadBD(); w.M1 = br.ReadBD();
            st.Margins.Add(w.M0); st.Margins.Add(w.M1);
            for (int i = 0; i < 4; i++) st.Margins.Add(br.ReadBD());
        }
        st.ContentFormat = cf;
        int numBorders = st.NumBorders = br.ReadBL();
        if (numBorders is < 0 or > 6)
        { w.Bail = $"num_borders={numBorders}"; return false; }
        w.NumBorders = numBorders;
        if (numBorders != 6) w.Thin = false;
        for (int k = 0; k < numBorders; k++)
        {
            var bd = new TableStyleBorder();
            int mask = bd.IndexMask = br.ReadBL();
            if (mask == 0)
            {
                w.Thin = false;
                st.Borders.Add(bd);
                continue;
            }
            bd.BorderOverrides = br.ReadBL();
            w.Vis[k] = (bd.Visible = br.ReadBL()) != 0;
            w.Col[k] = DwgObjectReader.ReadCmcTriple(br);
            bd.Color = ToCmc(w.Col[k]);
            w.Lw[k] = bd.LineWeight = br.ReadBL();
            bd.BorderLineType = br.ReadBL();
            bd.DoubleLineSpacing = br.ReadBD();
            st.Borders.Add(bd);
        }
        return true;
    }

    private static TableCmcColor ToCmc((int Idx, uint Rgb, byte Flag) c)
        => new((short)c.Idx, c.Rgb, "", "");

    private static void MapRows(TableStyleCapture ts, CellStyleWalk?[] byBl3)
    {
        var data = byBl3[2];
        var title = byBl3[0];
        var header = byBl3[1];
        if (data is not null) CopyTableStyleRow(ts.Rows[0], data);
        if (title is not null) CopyTableStyleRow(ts.Rows[1], title);
        if (header is not null) CopyTableStyleRow(ts.Rows[2], header);
    }

    private static void CopyTableStyleRow(TableStyleRowCapture dst, CellStyleWalk src)
    {
        dst.TextHeight = src.TextHeight;
        dst.Alignment = src.Alignment;
        dst.DataType = src.Vdt; dst.UnitType = src.Vut;
        dst.TextColor = src.Content; dst.FillColor = src.Bg;
        System.Array.Copy(src.Lw, dst.BorderLw, 6);
        System.Array.Copy(src.Col, dst.BorderCol, 6);
        System.Array.Copy(src.Vis, dst.BorderVis, 6);
    }
}
