using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    internal static void InferLayerColors(WalkResult result)
    {
        var layerExplicitColors = new Dictionary<ulong, List<short>>();
        foreach (var e in result.Entities)
        {
            if (e.LayerHandle == 0) continue;
            short ci = e.ColorIndex;
            if (ci is 0 or 256 || ci > 255) continue;
            if (!layerExplicitColors.TryGetValue(e.LayerHandle, out var list))
                layerExplicitColors[e.LayerHandle] = list = new List<short>();
            list.Add(ci);
        }
        foreach (var (layerHdl, colors) in layerExplicitColors)
        {
            if (result.LayerColors.ContainsKey(layerHdl) && result.LayerColors[layerHdl] != 0)
                continue;
            var grouped = colors.GroupBy(c => c).OrderByDescending(g => g.Count());
            result.LayerColors[layerHdl] = grouped.First().Key;
        }
    }

    private static bool IsNameByte(byte b)
    {
        char c = (char)b;
        return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '*' || c == '-' || c == '$';
    }

    private static void DecodeStyle(BitReader br, ulong handle,
        WalkResult result, DwgVersion version, DwgObjectRecord? record, int objStart)
    {
        if (record == null) return;
        ObjectFactory.Decode(
            ObjectFactory.CreateStyle(handle, version, result, record, objStart), br);
    }

    private static void DecodeTrailingNameRecord(BitReader br, DwgVersion version,
        DwgObjectRecord? record, int objStart, bool r2010Plus)
    {
        if (record == null || r2010Plus
            || version < DwgVersion.AC1015 || version >= DwgVersion.AC1021) return;
        bool r2004Plus = version.IsR2004Plus();
        uint bitsize = br.ReadRL();
        long hs = (long)objStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8) return;
        long ownStart = br.BitPosition;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!TrySkipEed(br)) return;
        long eedLen = br.BitPosition - eedStart;
        br.ReadBL();
        if (r2004Plus) br.ReadB();
        long strStart = br.BitPosition;
        string name = br.ReadTV();
        if (name.Length == 0)
        {
            long altStart = br.BitPosition;
            string alt = br.ReadTV();
            if (alt.Length > 0 && br.BitPosition <= hs) { strStart = altStart; name = alt; }
        }
        long strEnd = br.BitPosition;
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine($"NAMEREC h={record.Handle:X} t={record.TypeCode} hs={hs} strStart={strStart} strEnd={strEnd} gap={(int)(hs - strEnd)}b name='{name}'");
        if (strEnd > hs || name.Length == 0 || strStart < ownStart) return;
        record.HandleStreamStartBit = hs;
        record.HasHandleStream = true;
        record.EedStartBit = eedStart;
        record.EedBitLen = (int)eedLen;
        record.StringFields.Add(new DwgObjectRecord.StringField(strStart, (int)(strEnd - strStart), name));
    }

    private static void DecodeCellStyleMapStrings(BitReader br, DwgVersion version,
        DwgObjectRecord? record, int objStart)
    {
        if (version == DwgVersion.AC1021 && record != null)
        {
            long entry = br.BitPosition;
            var peek = new BitReader(br.UnderlyingData(), 0);
            DwgObjectRecord.SeekTo(peek, entry);
            long hs2 = (long)objStart * 8 + peek.ReadRL();
            long ssStart = Ac1021StringAreaStart(br.UnderlyingData(), hs2);
            if (ssStart > 0)
            {
                var bridge = new BitReader(br.UnderlyingData()) { UnicodeStrings = true };
                bridge.SetPosition((int)(ssStart >> 3), (int)(ssStart & 7));
                BitReader? old = br.StringStream;
                br.StringStream = bridge;
                try { DecodeCellStyleMapStringsCore(br, version, record, objStart); }
                finally { br.StringStream = old; br.SetPosition((int)(entry >> 3), (int)(entry & 7)); }
                return;
            }
        }
        DecodeCellStyleMapStringsCore(br, version, record, objStart);
    }

    private static void DecodeCellStyleMapStringsCore(BitReader br, DwgVersion version,
        DwgObjectRecord? record, int objStart)
    {
        if (record == null || version < DwgVersion.AC1015) return;
        bool central = version >= DwgVersion.AC1021;
        long hs, stop;
        if (central)
        {
            if (version.IsR2010Plus())
            {
                hs = record.HandleStreamStartBit;
                if (hs <= 0 || !record.HasHandleStream) return;
            }
            else
            {
                uint bitsize = br.ReadRL();
                hs = (long)objStart * 8 + bitsize;
            }
            if (version.IsR2010Plus())
            {
                stop = record.StringStreamStartBit;
                if (stop <= 0 || stop > hs) return;
            }
            else
            {
                stop = Ac1021StringAreaStart(br.UnderlyingData(), hs);
                if (stop <= 0 || stop >= hs) return;
            }
        }
        else
        {
            uint bitsize = br.ReadRL();
            hs = (long)objStart * 8 + bitsize;
            stop = hs;
        }
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8) return;
        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        void Dbg(string s) { if (dbg) System.Console.Error.WriteLine("CSMWALK " + s); }
        Dbg($"h={record.Handle:X} central={central} ver={version} pos={br.BitPosition} " +
            $"stop={stop} hs={hs} ss={record.StringStreamStartBit}");
        long ownStart = br.BitPosition;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        bool eedOk = TrySkipEed(br);
        Dbg($"own->{eedStart} eedOk={eedOk} afterEed={br.BitPosition}");
        if (!eedOk) return;
        long eedLen = br.BitPosition - eedStart;
        int numReactors = br.ReadBL();
        Dbg($"nr={numReactors} pos={br.BitPosition}");
        if (numReactors is < 0 or > 8) return;
        if (version >= DwgVersion.AC1018) br.ReadB();
        if (version >= DwgVersion.AC1027) br.ReadB();
        Dbg($"xdic@{br.BitPosition}");
        var fields = new List<DwgObjectRecord.StringField>();
        var inserts = central ? new List<CellStyleMapString>() : null;
        var cells = new List<CellStyleMapCell>();
        int numCells = br.ReadBL();
        Dbg($"cells={numCells} pos={br.BitPosition}");
        if (numCells is < 0 or > 64) return;
        for (int c = 0; c < numCells; c++)
        {
            var cell = new CellStyleMapCell();
            int cellType = br.ReadBL();
            if (cellType is < 0 or > 255) return;
            cell.Type = cellType;
            int df = br.ReadBS();
            if (df < 0) return;
            cell.DataFlags = df;
            if (df != 0)
            {
                cell.PropertyOverrideFlags = br.ReadBL();
                cell.MergeFlags = br.ReadBL();
                var bg = ReadCmcTriple(br);
                cell.BgColor = new CmcTriple(bg.Idx, bg.Rgb, bg.Flag);
                cell.ContentLayout = br.ReadBL();
                var cf = cell.ContentFormat = new CellStyleMapContentFormat();
                cf.PropertyOverrideFlags = br.ReadBL();
                cf.PropertyFlags = br.ReadBL();
                cf.ValueDataType = br.ReadBL();
                cf.ValueUnitType = br.ReadBL();
                long s0 = br.BitPosition;
                string vf = br.ReadTV();
                if (vf == null || vf.Length > 1024 || br.BitPosition > stop) return;
                cf.ValueFormatString = vf;
                if (central) inserts!.Add(new CellStyleMapString(s0, vf));
                else fields.Add(new DwgObjectRecord.StringField(s0, (int)(br.BitPosition - s0), vf));
                double rot = br.ReadBD();
                if (double.IsNaN(rot)) return;
                cf.Rotation = rot;
                double bsc = br.ReadBD();
                if (double.IsNaN(bsc)) return;
                cf.BlockScale = bsc;
                cf.CellAlignment = br.ReadBL();
                var cc = ReadCmcTriple(br);
                cf.ContentColor = new CmcTriple(cc.Idx, cc.Rgb, cc.Flag);
                double th = br.ReadBD();
                if (double.IsNaN(th)) return;
                cf.TextHeight = th;
                int movf = br.ReadBS();
                if (movf < 0) return;
                cell.MarginOverrideFlags = movf;
                if (movf != 0)
                    for (int m = 0; m < 6; m++)
                    {
                        double mv = br.ReadBD();
                        if (double.IsNaN(mv)) return;
                        cell.Margins[m] = mv;
                    }
                int nb = br.ReadBL();
                if (nb is < 0 or > 6) return;
                for (int b = 0; b < nb; b++)
                {
                    int mask = br.ReadBL();
                    if (mask < 0) return;
                    if (mask == 0) continue;
                    var bd = new CellStyleMapBorder { IndexMask = mask };
                    bd.BorderOverrides = br.ReadBL();
                    bd.BorderType = br.ReadBL();
                    var bc = ReadCmcTriple(br);
                    bd.Color = new CmcTriple(bc.Idx, bc.Rgb, bc.Flag);
                    bd.LineWeight = br.ReadBL();
                    bd.Visible = br.ReadBL();
                    double dls = br.ReadBD();
                    if (double.IsNaN(dls)) return;
                    bd.DoubleLineSpacing = dls;
                    cell.Borders.Add(bd);
                }
            }
            cell.Id = br.ReadBL();
            cell.EntryType = br.ReadBL();
            long n0 = br.BitPosition;
            string name = br.ReadTV();
            if (name == null || name.Length > 255 || br.BitPosition > stop) return;
            if (central) inserts!.Add(new CellStyleMapString(n0, name));
            else
            {
                if (string.IsNullOrEmpty(name)) return;
                fields.Add(new DwgObjectRecord.StringField(n0, (int)(br.BitPosition - n0), name));
            }
            cell.Name = name;
            cells.Add(cell);
            Dbg($"cell{c} df={df} name='{name}' end={br.BitPosition}");
        }
        Dbg($"final pos={br.BitPosition} stop={stop} delta={br.BitPosition - stop}");
        if (br.BitPosition != stop) return;
        if (ownStart < 0) return;
        if (!central || version.IsR2010Plus())
        {
            (record.CellStyleMap ??= new CellStyleMapCapture()).ValuesCommitted = true;
            record.CellStyleMap.NumCells = numCells;
            record.CellStyleMap.Cells.AddRange(cells);
        }
        if (central)
        {
            if (inserts!.Count == 0) return;
            var cap = record.CellStyleMap ??= new CellStyleMapCapture();
            cap.NumCells = numCells;
            foreach (var s in inserts) cap.Strings.Add(s);
            cap.SurgeryCommitted = true;
            if (version.IsR2010Plus()) cap.Captured = true;
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                System.Console.Error.WriteLine(
                    $"CSMDOWN h={record.Handle:X} t={record.TypeCode} {version} cells={numCells} " +
                    $"ins={inserts.Count} own={ownStart} stop={stop} hs={hs} eed={eedStart}+{eedLen} " +
                    $"[{string.Join(",", inserts.Select(x => $"'{x.Value}'@{x.Pos}"))}]");
            return;
        }
        if (fields.Count == 0) return;
        record.HandleStreamStartBit = hs;
        record.HasHandleStream = true;
        record.EedStartBit = eedStart;
        record.EedBitLen = (int)eedLen;
        foreach (var f in fields) record.StringFields.Add(f);
        (record.CellStyleMap ??= new CellStyleMapCapture()).SurgeryCommitted = true;
    }

    private static long Ac1021StringAreaStart(byte[] data, long hs)
    {
        var fr = new BitReader(data, 0);
        DwgObjectRecord.SeekTo(fr, hs - 1);
        if (!fr.ReadB()) return 0;
        DwgObjectRecord.SeekTo(fr, hs - 17);
        uint dataSize = fr.ReadRS();
        int sizeFieldBits = 17;
        if ((dataSize & 0x8000) != 0)
        {
            DwgObjectRecord.SeekTo(fr, hs - 33);
            dataSize = (dataSize & 0x7FFF) | ((uint)fr.ReadRS() << 15);
            sizeFieldBits = 33;
        }
        long start = hs - sizeFieldBits - dataSize;
        return start > 0 && start < hs ? start : 0;
    }

    private static void DecodeLType(BitReader br, ulong handle,
        DwgVersion version, DwgObjectRecord? record, int objStart)
    {
        if (record == null) return;
        var lt = ObjectFactory.CreateLtype(handle, version, record, objStart);
        if (lt != null) ObjectFactory.Decode(lt, br);
    }

    private static void DecodeDimStyle(BitReader br, ulong handle,
        WalkResult result, DwgVersion version, DwgObjectRecord? record, int objStart)
    {
        if (record == null) return;
        ObjectFactory.Decode(
            ObjectFactory.CreateDimStyle(handle, version, result, record, objStart), br);
    }


    internal static (int Idx, uint Rgb, byte Flag) ReadCmcTriple(BitReader br)
    {
        int idx = br.ReadBS();
        uint rgb = unchecked((uint)br.ReadBL());
        byte flag = br.ReadRC();
        if (flag < 4)
        {
            if ((flag & 1) != 0) br.ReadTV();
            if ((flag & 2) != 0) br.ReadTV();
        }
        return (idx, rgb, flag);
    }



}
