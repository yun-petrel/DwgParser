using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class TableStyleAcR2000toR2007 : TableStyleRecord
{
    public override string Family => "R2000–R2007";

    public TableStyleAcR2000toR2007(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    private bool Bail(string at, BitReader br)
    {
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"TSUP h={Rec.Handle:X} src={SrcVersion} bail={at} pos={br.BitPosition}");
        return false;
    }

    public override bool TryCapture(BitReader br)
    {
        bool ac1021 = SrcVersion >= DwgVersion.AC1021;
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return Bail("rl-hs", br);
        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        br.ReadHandle();
        Rec.EedStartBit = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return Bail("eed", br);
        Rec.EedBitLen = (int)(br.BitPosition - Rec.EedStartBit);
        int numReactors = br.ReadBL();
        bool xdicMissing = SrcVersion.IsR2004Plus() && br.ReadB();
        if (numReactors is < 0 or > 8) return Bail("nreact", br);
        if (ac1021)
        {
            DwgObjectReader.AttachStringStream(br, ObjStart, bitsize, SrcVersion, Rec);
            if (Rec.StringStreamStartBit <= 0) return Bail("ss", br);
        }
        long headEnd = br.BitPosition;
        string name = br.ReadTV();
        if (string.IsNullOrEmpty(name) || name.Length > 255) return Bail("name", br);
        int flow = br.ReadBS();
        int flags = br.ReadBS();
        double horiz = br.ReadBD();
        double vert = br.ReadBD();
        bool titleSup = br.ReadB();
        bool headerSup = br.ReadB();
        var rows = Cap.Rows;
        for (int r = 0; r < 3; r++)
        {
            var row = rows[r];
            row.TextHeight = br.ReadBD();
            row.Alignment = br.ReadBS();
            row.TextColor = DwgObjectReader.ReadCmcTriple(br);
            row.FillColor = DwgObjectReader.ReadCmcTriple(br);
            row.HasBg = br.ReadB();
            for (int k = 0; k < 6; k++)
            {
                row.BorderLw[k] = br.ReadBS();
                row.BorderVis[k] = br.ReadB();
                row.BorderCol[k] = DwgObjectReader.ReadCmcTriple(br);
            }
            if (ac1021)
            {
                row.DataType = br.ReadBL();
                row.UnitType = br.ReadBL();
            }
        }
        long stop = ac1021 ? Rec.StringStreamStartBit : hs;
        if (br.BitPosition != stop) return Bail("stop", br);
        if (flow is < 0 or > 1024) return Bail("hdr-flow", br);
        if (flags is < 0 or > 1024) return Bail("hdr-flags-range", br);
        Cap.FlowDirection = flow;
        Cap.Flags = flags;
        Cap.TitleSuppressed = titleSup ? 1 : 0;
        Cap.HeaderSuppressed = headerSup ? 1 : 0;
        Cap.HeaderNonDefault = flow != 0 || flags != 0 || titleSup || headerSup;
        foreach (var row in rows)
        {
            if (row.TextHeight is double.NaN or <= 0 or > 1e6) return Bail("row.th", br);
            if (row.Alignment is < 0 or > 255) return Bail("row.align", br);
            if (!IsPlainColor(row.TextColor) || !IsPlainColor(row.FillColor)) return Bail("row.col", br);
            for (int k = 0; k < 6; k++)
            {
                if (row.BorderLw[k] < -4) return Bail("row.lw", br);
                if (!IsPlainColor(row.BorderCol[k])) return Bail("row.bcol", br);
            }
            if (ac1021 && (row.DataType < 0 || row.DataType > 1024 ||
                            row.UnitType < 0 || row.UnitType > 200)) return Bail("row.dt", br);
        }
        DwgObjectRecord.SeekTo(br, hs);
        br.ReadHandle();
        for (int i = 0; i < numReactors; i++) br.ReadHandle();
        if (!xdicMissing) br.ReadHandle();
        long commonEnd = br.BitPosition;
        var tsty = new ulong[3];
        for (int i = 0; i < 3; i++)
        {
            var h = br.ReadHandle();
            if (h.Code != 5) return Bail("tsty", br);
            tsty[i] = h.Value;
        }
        long frameEnd = (long)Rec.BodyEnd * 8;
        if (frameEnd - br.BitPosition >= 8)
            for (long p = br.BitPosition; p < frameEnd; p++)
                if (br.ReadB()) return Bail("tail", br);
        Cap.Captured = true;
        Cap.Name = name;
        Cap.HorizMargin = horiz;
        Cap.VertMargin = vert;
        Cap.HeadEndBit = headEnd;
        Cap.HsCommonEndBit = commonEnd;
        System.Array.Copy(tsty, Cap.Tsty, 3);
        return true;
    }

    private static bool IsPlainColor((int Idx, uint Rgb, byte Flag) c)
        => c.Idx == 0 && c.Flag == 0;
}
