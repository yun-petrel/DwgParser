using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class TableAcPreR2010 : TableEntity
{
    public override void ReadBody(BitReader br, DwgVersion version, DwgObjectRecord? record)
    {
        var ins = br.Read3BD();
        double sx = 1.0, sy = 1.0, sz = 1.0;
        byte scaleFlag = br.ReadBB();
        switch (scaleFlag)
        {
            case 0: sx = br.ReadRD(); sy = br.ReadDD(sx); sz = br.ReadDD(sx); break;
            case 1: sy = br.ReadDD(1.0); sz = br.ReadDD(1.0); break;
            case 2: sx = br.ReadRD(); sy = sx; sz = sx; break;
            case 3: break;
        }
        double rotation = br.ReadBD0();
        var extrusion = br.ReadBE();
        bool hasAttribs = br.ReadB();
        short flag = br.ReadBS();
        var dir = br.Read3BD();
        int cols = br.ReadBL();
        int rows = br.ReadBL();
        TableColumns = (short)cols;
        TableRows = (short)rows;

        Kind = DwgEntityType.Table;
        Start = new Vec2(ins.X, ins.Y);
        Rotation = rotation;
        ScaleX = sx;
        ScaleY = sy;
        ScaleZ = sz;

        TableFlag = flag;
        HorizDirection = new Vec2(dir.X, dir.Y);
        if (cols is >= 0 and <= 5000 && rows is >= 0 and <= 5000)
        {
            try
            {
                var cw = new double[cols];
                for (int i = 0; i < cols; i++) cw[i] = br.ReadBD();
                var rh = new double[rows];
                for (int i = 0; i < rows; i++) rh[i] = br.ReadBD();
                ColWidths.Clear(); ColWidths.AddRange(cw);
                RowHeights.Clear(); RowHeights.AddRange(rh);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException or OverflowException)
            {
            }
        }

        bool r2007Plus = (int)version >= (int)DwgVersion.AC1021;
        if (cols is < 0 or > 5000 || rows is < 0 or > 5000) return;
        if (r2007Plus && record is not { StringStreamStartBit: > 0 }) return;
        try
        {
            var cells = new List<TableEntityCell>();
            long numCells = (long)cols * rows;
            if (numCells is < 0 or > 250000) throw new EndOfStreamException();
            long cellsStart = br.BitPosition;
            for (long i = 0; i < numCells; i++)
                cells.Add(ReadCell(br, r2007Plus));
            long cellsEnd = br.BitPosition;

            TableStyleOverride? style = ReadStyleOverride(br);
            var borders = new List<TableBorderOverride>(3);
            for (int g = 0; g < 3; g++)
            {
                if (!br.ReadB()) continue;
                borders.Add(ReadBorderGroup(br, g == 0));
            }

            long expectedEnd = r2007Plus
                ? record!.StringStreamStartBit
                : record is { HandleStreamStartBit: > 0 } ? record.HandleStreamStartBit : -1;
            if (expectedEnd > 0 && br.BitPosition != expectedEnd)
                return;

            Cells.Clear(); Cells.AddRange(cells);
            StyleOverride = style;
            BorderOverrides.Clear(); BorderOverrides.AddRange(borders);

            if (record != null)
            {
                record.Table = Capture(ins, scaleFlag, sx, sy, sz, rotation, extrusion,
                    hasAttribs, cols, rows, dir, style);
                record.TableCellsStartBit = cellsStart;
                record.TableCellsEndBit = cellsEnd;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException or OverflowException)
        {
        }
    }

    private Records.TableCapture Capture(
        (double X, double Y, double Z) ins, byte scaleFlag, double sx, double sy, double sz,
        double rotation, (double X, double Y, double Z) extrusion, bool hasAttribs,
        int cols, int rows, (double X, double Y, double Z) dir, TableStyleOverride? style)
    {
        var cap = new Records.TableCapture
        {
            Captured = true,
            InsX = ins.X, InsY = ins.Y, InsZ = ins.Z,
            NormalX = extrusion.X, NormalY = extrusion.Y, NormalZ = extrusion.Z,
            ScaleFlag = scaleFlag, ScaleX = sx, ScaleY = sy, ScaleZ = sz,
            Rotation = rotation, HasAttribs = hasAttribs,
            Columns = cols, Rows = rows,
            HorizDirX = dir.X, HorizDirY = dir.Y, HorizDirZ = dir.Z,
            TitleSuppressed = style?.TitleSuppressed ?? false,
        };
        cap.ColWidths.AddRange(ColWidths);
        cap.RowHeights.AddRange(RowHeights);
        cap.Cells.AddRange(Cells);
        if (style is not null)
        {
            cap.HorzCellMargin = style.HorzCellMargin ?? 0.0;
            cap.VertCellMargin = style.VertCellMargin ?? 0.0;
            cap.DataRowHeight = style.DataRowHeight ?? 0.0;
        }
        return cap;
    }

    private static TableCmcColor ReadCmcColor(BitReader br)
    {
        short idx = DwgObjectReader.ReadCmc(br, out uint rgb, out string name, out string book);
        return new TableCmcColor(idx, rgb, name, book);
    }

    private static TableEntityCell ReadCell(BitReader br, bool r2007Plus)
    {
        var c = new TableEntityCell();
        c.CellStartBit = br.BitPosition;
        c.Type = br.ReadBS();
        c.EdgeFlags = br.ReadRC();
        c.IsMergedValue = br.ReadB();
        c.IsAutoFit = br.ReadB();
        c.MergedWidth = br.ReadBL();
        c.MergedHeight = br.ReadBL();
        c.Rotation = br.ReadBD();
        switch (c.Type)
        {
            case 1:
                if (!r2007Plus)
                {
                    c.TextStartBit = br.BitPosition;
                    c.Text = br.ReadTV();
                    c.TextEndBit = br.BitPosition;
                }
                break;
            case 2:
                c.BlockScale = br.ReadBD();
                if (br.ReadB())
                {
                    int natts = br.ReadBS();
                    if (natts is < 0 or > 4096) throw new EndOfStreamException();
                    for (int a = 0; a < natts; a++)
                    {
                        var ah = br.ReadHandle();
                        short aidx = br.ReadBS();
                        string atxt = br.ReadTV();
                        c.AttrDefs.Add(new TableCellAttrDef
                        {
                            AttdefHandleCode = ah.Code,
                            AttdefHandle = ah.Value,
                            Index = aidx,
                            Text = atxt,
                        });
                    }
                }
                break;
        }
        c.FlatEndBit = br.BitPosition;
        if (br.ReadB())
        {
            c.HasOverride = true;
            c.CellFlagOverride = br.ReadBL();
            c.VirtualEdgeFlag = br.ReadRC();
            int f = c.CellFlagOverride;
            if ((f & 0x01) != 0) c.Alignment = br.ReadBS();
            if ((f & 0x02) != 0) c.BgFillNone = br.ReadB();
            if ((f & 0x04) != 0) c.BgColor = ReadCmcColor(br);
            if ((f & 0x08) != 0) c.ContentColor = ReadCmcColor(br);
            if ((f & 0x20) != 0) c.TextHeight = br.ReadBD();
            if ((f & 0x00040) != 0) c.TopColor = ReadCmcColor(br);
            if ((f & 0x00400) != 0) c.TopLineWeight = br.ReadBS();
            if ((f & 0x04000) != 0) c.TopVisibility = br.ReadBS();
            if ((f & 0x00080) != 0) c.RightColor = ReadCmcColor(br);
            if ((f & 0x00800) != 0) c.RightLineWeight = br.ReadBS();
            if ((f & 0x08000) != 0) c.RightVisibility = br.ReadBS();
            if ((f & 0x00100) != 0) c.BottomColor = ReadCmcColor(br);
            if ((f & 0x01000) != 0) c.BottomLineWeight = br.ReadBS();
            if ((f & 0x10000) != 0) c.BottomVisibility = br.ReadBS();
            if ((f & 0x00200) != 0) c.LeftColor = ReadCmcColor(br);
            if ((f & 0x02000) != 0) c.LeftLineWeight = br.ReadBS();
            if ((f & 0x20000) != 0) c.LeftVisibility = br.ReadBS();
        }
        c.ValueUnionStartBit = r2007Plus ? br.BitPosition : -1;
        if (r2007Plus) ReadValueUnion(br, c);
        c.CellEndBit = br.BitPosition;
        return c;
    }

    private static void ReadValueUnion(BitReader br, TableEntityCell c)
    {
        br.ReadBL();
        c.ValueFormatFlags = br.ReadBL();
        c.ValueDataType = br.ReadBL();
        switch (c.ValueDataType)
        {
            case 0: break;
            case 1:
            case 512: c.ValueLong = br.ReadBL(); break;
            case 2: c.ValueDouble = br.ReadBD(); break;
            case 4:
            {
                int lenBytes = br.ReadBS();
                if (lenBytes is < 0 or > 0x10000) throw new EndOfStreamException();
                var bytes = new byte[lenBytes];
                for (int i = 0; i < lenBytes; i++) bytes[i] = br.ReadRC();
                c.Text = System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                break;
            }
            case 8:
            {
                int sz = br.ReadBL();
                if (sz is < 0 or > 0x10000) throw new EndOfStreamException();
                var date = new byte[sz];
                for (int i = 0; i < sz; i++) date[i] = br.ReadRC();
                c.ValueDateBytes = date;
                break;
            }
            case 16: if (br.ReadBL() > 0) { double x = br.ReadRD(), y = br.ReadRD(); c.ValuePoint2D = new Vec2(x, y); } break;
            case 32: if (br.ReadBL() > 0) { double x = br.ReadRD(), y = br.ReadRD(), z = br.ReadRD(); c.ValuePoint3D = new Vec3(x, y, z); } break;
            case 64: break;
            default: throw new EndOfStreamException($"cell value type {c.ValueDataType}");
        }
        c.ValueUnitType = br.ReadBL();
        c.ValueFormat = br.ReadTV();
        if (c.ValueUnitType != 12) br.ReadTV();
    }

    private static TableStyleOverride? ReadStyleOverride(BitReader br)
    {
        if (!br.ReadB()) return null;
        var so = new TableStyleOverride();
        so.Flag = br.ReadBL();
        int f = so.Flag;
        if ((f & 0x0001) != 0) so.TitleSuppressed = br.ReadB();
        if ((f & 0x0004) != 0) so.FlowDirection = br.ReadBS();
        if ((f & 0x0008) != 0) so.HorzCellMargin = br.ReadBD();
        if ((f & 0x0010) != 0) so.VertCellMargin = br.ReadBD();
        if ((f & 0x0020) != 0) so.TitleRowColor = ReadCmcColor(br);
        if ((f & 0x0040) != 0) so.HeaderRowColor = ReadCmcColor(br);
        if ((f & 0x0080) != 0) so.DataRowColor = ReadCmcColor(br);
        if ((f & 0x0100) != 0) so.TitleFillNone = br.ReadB();
        if ((f & 0x0200) != 0) so.HeaderFillNone = br.ReadB();
        if ((f & 0x0400) != 0) so.DataFillNone = br.ReadB();
        if ((f & 0x0800) != 0) so.TitleFillColor = ReadCmcColor(br);
        if ((f & 0x1000) != 0) so.HeaderFillColor = ReadCmcColor(br);
        if ((f & 0x2000) != 0) so.DataFillColor = ReadCmcColor(br);
        if ((f & 0x4000) != 0) so.TitleRowAlignment = br.ReadBS();
        if ((f & 0x8000) != 0) so.HeaderRowAlignment = br.ReadBS();
        if ((f & 0x10000) != 0) so.DataRowAlignment = br.ReadBS();
        if ((f & 0x100000) != 0) so.TitleRowHeight = br.ReadBD();
        if ((f & 0x200000) != 0) so.HeaderRowHeight = br.ReadBD();
        if ((f & 0x400000) != 0) so.DataRowHeight = br.ReadBD();
        return so;
    }

    private static TableBorderOverride ReadBorderGroup(BitReader br, bool isColorGroup)
    {
        var bo = new TableBorderOverride();
        bo.Flag = br.ReadBL();
        for (int b = 0; b < 18; b++)
        {
            if ((bo.Flag & (1 << b)) == 0) continue;
            if (isColorGroup)
            {
                short idx = DwgObjectReader.ReadCmc(br, out uint rgb, out string nm, out string bk);
                bo.Values.Add(idx);
                bo.Rgbs.Add(rgb);
                bo.ColorNames.Add(nm);
                bo.ColorBookNames.Add(bk);
            }
            else
            {
                bo.Values.Add(br.ReadBS());
            }
        }
        return bo;
    }
}
