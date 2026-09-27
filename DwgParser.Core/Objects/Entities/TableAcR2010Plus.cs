using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class TableAcR2010Plus : TableEntity
{
    public override void ReadBody(BitReader br, DwgVersion version, DwgObjectRecord? record)
    {
        Kind = DwgEntityType.Table;
        TryWalkBody(br, r2013Plus: version >= DwgVersion.AC1027, record);
    }

    private bool TryWalkBody(BitReader br, bool r2013Plus, DwgObjectRecord? record)
    {
        long startBit = br.BitPosition;
        var cells = new List<TableEntityCell>();
        try
        {
            var ins = br.Read3BD();
            double sx = 1.0, sy = 1.0, sz = 1.0;
            byte sf = br.ReadBB();
            switch (sf)
            {
                case 0: sx = br.ReadRD(); sy = br.ReadDD(sx); sz = br.ReadDD(sx); break;
                case 1: sy = br.ReadDD(1.0); sz = br.ReadDD(1.0); break;
                case 2: sx = br.ReadRD(); sy = sx; sz = sx; break;
                case 3: break;
            }
            double rotation = br.ReadBD();
            var normal = br.Read3BD();
            bool hasAttribs = br.ReadB();
            int numOwned = 0;
            if (hasAttribs)
                numOwned = br.ReadBL();

            br.ReadRC();
            br.ReadBL();
            if (r2013Plus) br.ReadBL(); else br.ReadB();
            string name = br.ReadTV();
            string description = br.ReadTV();

            int numCols = br.ReadBL();
            var colWidths = new double[numCols];
            var colNames = new List<string>(numCols);
            var colStyles = new List<TableCellStyle>(numCols);
            var colCustomRefs = new List<int>(numCols);
            for (int i = 0; i < numCols; i++)
            {
                colNames.Add(br.ReadTV());
                br.ReadBL();
                int nd = br.ReadBL();
                int cd = 0;
                for (int j = 0; j < nd; j++) cd += SkipCustomData(br);
                colCustomRefs.Add(cd);
                colStyles.Add(ReadCellStyle(br));
                br.ReadBL();
                colWidths[i] = br.ReadBD();
            }

            int numRows = br.ReadBL();
            var rowHeights = new double[numRows];
            var rowStyles = new List<TableCellStyle>(numRows);
            var rowCellCounts = new List<int>(numRows);
            var rowCustomRefs = new List<int>(numRows);
            for (int i = 0; i < numRows; i++)
            {
                int ncells = br.ReadBL();
                rowCellCounts.Add(ncells);
                for (int j = 0; j < ncells; j++) cells.Add(ReadCell(br));
                br.ReadBL();
                int nd = br.ReadBL();
                int cd = 0;
                for (int j = 0; j < nd; j++) cd += SkipCustomData(br);
                rowCustomRefs.Add(cd);
                rowStyles.Add(ReadCellStyle(br));
                br.ReadBL();
                rowHeights[i] = br.ReadBD();
            }

            int numFieldRefs = br.ReadBL();
            for (int i = 0; i < numFieldRefs; i++) {  }
            var baseStyle = ReadCellStyle(br);
            int numMerged = br.ReadBL();
            var mergedCells = new List<TableMergedCell>(numMerged);
            for (int i = 0; i < numMerged; i++)
            {
                int top = br.ReadBL(), left = br.ReadBL();
                int bottom = br.ReadBL(), right = br.ReadBL();
                mergedCells.Add(new TableMergedCell(top, left, bottom, right));
            }

            br.ReadBS();
            var horizDir = br.Read3BD();
            if (br.ReadBL() == 1)
            {
                br.ReadBL(); br.ReadBL(); br.ReadBD(); br.ReadBL(); br.ReadBL();
                int nh = br.ReadBL();
                for (int i = 0; i < nh; i++) { br.Read3BD(); br.ReadBD(); br.ReadBL(); }
            }
            int numBreakRows = br.ReadBL();
            for (int i = 0; i < numBreakRows; i++) { br.Read3BD(); br.ReadBL(); br.ReadBL(); }

            Start = new Vec2(ins.X, ins.Y);
            Rotation = rotation;
            ScaleX = sx; ScaleY = sy; ScaleZ = sz;
            TableColumns = (short)numCols;
            TableRows = (short)numRows;
            ColWidths.Clear(); ColWidths.AddRange(colWidths);
            RowHeights.Clear(); RowHeights.AddRange(rowHeights);
            HorizDirection = new Vec2(horizDir.X, horizDir.Y);
            if (record is { StringStreamStartBit: > 0 } rec10 && br.BitPosition == rec10.StringStreamStartBit
                || record is { HandleStreamStartBit: > 0 } rec10b && record.StringStreamStartBit == 0
                   && br.BitPosition == rec10b.HandleStreamStartBit - 18)
            {
                Cells.Clear();
                Cells.AddRange(cells);
                TableName = name;
                Description = description;
                NumOwned = hasAttribs ? numOwned : null;
                ColumnNames.Clear(); ColumnNames.AddRange(colNames);
                ColumnStyles.Clear(); ColumnStyles.AddRange(colStyles);
                RowStyles.Clear(); RowStyles.AddRange(rowStyles);
                BaseCellStyle = baseStyle;
                MergedCells.Clear(); MergedCells.AddRange(mergedCells);
                NumFieldRefs = numFieldRefs;
                HsColCustomDataRefs.Clear(); HsColCustomDataRefs.AddRange(colCustomRefs);
                HsRowCustomDataRefs.Clear(); HsRowCustomDataRefs.AddRange(rowCustomRefs);
                HsRowCellCounts.Clear(); HsRowCellCounts.AddRange(rowCellCounts);
                PopulateDownCapture(record, ins, sf, sx, sy, sz, rotation, normal,
                    hasAttribs, numCols, numRows, horizDir);
            }
            return true;
        }
        catch (Exception)
        {
            br.SetPosition((int)(startBit >> 3), (int)(startBit & 7));
            return false;
        }
    }

    private void PopulateDownCapture(DwgObjectRecord record, (double X, double Y, double Z) ins,
        byte scaleFlag, double sx, double sy, double sz, double rotation,
        (double X, double Y, double Z) normal, bool hasAttribs, int cols, int rows,
        (double X, double Y, double Z) dir)
    {
        var cap = new Records.TableCapture
        {
            Captured = true,
            InsX = ins.X, InsY = ins.Y, InsZ = ins.Z,
            NormalX = normal.X, NormalY = normal.Y, NormalZ = normal.Z,
            ScaleFlag = scaleFlag, ScaleX = sx, ScaleY = sy, ScaleZ = sz,
            Rotation = rotation, HasAttribs = hasAttribs,
            Columns = cols, Rows = rows,
            HorizDirX = dir.X, HorizDirY = dir.Y, HorizDirZ = dir.Z,
            TitleSuppressed = false,
        };
        cap.ColWidths.AddRange(ColWidths);
        cap.RowHeights.AddRange(RowHeights);
        cap.Cells.AddRange(Cells);
        cap.MergedCells.AddRange(MergedCells);
        if (BaseCellStyle?.ContentFormat is { TextHeight: > 0 } baseCf) cap.BaseTextHeight = baseCf.TextHeight;
        record.Table = cap;
    }

    internal static TableCellStyle ReadCellStyle(BitReader b)
    {
        var st = new TableCellStyle();
        st.Type = b.ReadBL();
        st.DataFlags = b.ReadBS();
        if (st.DataFlags == 0) return st;
        st.PropertyOverrideFlags = b.ReadBL();
        st.MergeFlags = b.ReadBL();
        st.BackgroundColor = ReadCmcColor(b);
        st.ContentLayout = b.ReadBL();
        st.ContentFormat = ReadContentFormat(b);
        st.MarginOverrideFlags = b.ReadBS();
        if (st.MarginOverrideFlags != 0)
            for (int i = 0; i < 6; i++) st.Margins.Add(b.ReadBD());
        int nb = b.ReadBL();
        for (int i = 0; i < nb; i++)
        {
            int edge = b.ReadBL();
            if (edge == 0) continue;
            if (edge == 1 || edge == 2 || edge == 4 || edge == 8 || edge == 0x10 || edge == 0x20)
                st.Borders.Add(ReadBorder(b, edge));
        }
        return st;
    }

    private static TableContentFormat ReadContentFormat(BitReader b)
    {
        var f = new TableContentFormat();
        f.PropertyOverrideFlags = b.ReadBL();
        f.PropertyFlags = b.ReadBL();
        f.ValueDataType = b.ReadBL();
        f.ValueUnitType = b.ReadBL();
        f.ValueFormatString = b.ReadTV();
        f.Rotation = b.ReadBD();
        f.BlockScale = b.ReadBD();
        f.CellAlignment = b.ReadBL();
        f.ContentColor = ReadCmcColor(b);
        f.TextHeight = b.ReadBD();
        return f;
    }

    private static TableCellBorder ReadBorder(BitReader b, int indexMask)
    {
        var bd = new TableCellBorder { IndexMask = indexMask };
        bd.BorderOverrides = b.ReadBL();
        bd.BorderType = b.ReadBL();
        bd.Color = ReadCmcColor(b);
        bd.LineWeight = b.ReadBL();
        bd.Visible = b.ReadBL();
        bd.DoubleLineSpacing = b.ReadBD();
        return bd;
    }

    private static TableCmcColor ReadCmcColor(BitReader br)
    {
        short idx = DwgObjectReader.ReadCmc(br, out uint rgb, out string name, out string book);
        return new TableCmcColor(idx, rgb, name, book);
    }

    private static int ReadCadValue(BitReader b, bool r2007Plus, TableEntityCell? c)
    {
        int ff = 0;
        if (r2007Plus)
        {
            ff = b.ReadBL();
            if (c != null) c.ValueFormatFlags = ff;
        }
        int tc = b.ReadBL();
        if (c != null) c.ValueDataType = tc;
        int hsRefs = 0;
        switch (tc)
        {
            case 1: { var lv = b.ReadBL(); if (c != null) c.ValueLong = lv; break; }
            case 0: if (ff == 6) b.ReadBL(); break;
            case 2: { var d = b.ReadBD(); if (c != null) c.ValueDouble = d; break; }
            case 4: case 0x200:
            {
                int len = b.ReadBL();
                if (len is < 0 or > 0x200000) throw new EndOfStreamException();
                var bytes = new byte[len];
                for (int i = 0; i < len; i++) bytes[i] = b.ReadRC();
                if (c != null && r2007Plus && len >= 2)
                    c.Text = System.Text.Encoding.Unicode.GetString(bytes, 0, len - 2);
                break;
            }
            case 8:
            {
                int sz = b.ReadBL();
                var date = new List<byte>();
                for (int i = 0; i < sz; i++) date.Add(b.ReadRC());
                if (c != null) c.ValueDateBytes = date.ToArray();
                break;
            }
            case 0x10: if (b.ReadBL() > 0) { double x = b.ReadRD(), y = b.ReadRD(); if (c != null) c.ValuePoint2D = new Vec2(x, y); } break;
            case 0x20: if (b.ReadBL() > 0) { double x = b.ReadRD(), y = b.ReadRD(), z = b.ReadRD(); if (c != null) c.ValuePoint3D = new Vec3(x, y, z); } break;
            case 0x40: hsRefs = 1; break;
            default: break;
        }
        if (r2007Plus)
        {
            int unit = b.ReadBL();
            if (c != null) c.ValueUnitType = unit;
            var vfmt = b.ReadTV();
            if (c != null) c.ValueFormat = vfmt;
            if (unit != 12)
            {
                var vs = b.ReadTV();
                if (c != null) c.FormattedValue = vs;
            }
        }
        return hsRefs;
    }

    internal static int SkipCustomData(BitReader b)
    {
        bool r2007Plus = b.UnicodeStrings;
        b.ReadTV();
        return ReadCadValue(b, r2007Plus, null);
    }

    private static void ReadCellContent(BitReader b, TableEntityCell c, bool primary)
    {
        int ct = b.ReadBL();
        if (ct == 1)
        {
            if (primary && c.Type == 0) c.Type = 1;
            ReadCadValue(b, b.UnicodeStrings, primary ? c : null);
        }
        else if (ct == 4 && primary && c.Type == 0) c.Type = 2;
        int na = b.ReadBL();
        for (int i = 0; i < na; i++)
        {
            string av = b.ReadTV();
            int ai = b.ReadBL();
            c.AttrDefs.Add(new TableCellAttrDef { Index = ai, Text = av });
        }
        if (b.ReadBS() != 0) c.ContentFormat = ReadContentFormat(b);
    }

    internal static TableEntityCell ReadCell(BitReader b)
    {
        var c = new TableEntityCell();
        c.CellStartBit = b.BitPosition;
        c.CellFlagOverride = b.ReadBL();
        c.Tooltip = b.ReadTV();
        b.ReadBL();
        int nd = b.ReadBL();
        int cd = 0;
        for (int i = 0; i < nd; i++) cd += SkipCustomData(b);
        c.HsCustomDataRefs = cd;
        bool hasLink = b.ReadBL() != 0;
        c.HsHasDataLink = hasLink;
        if (hasLink)
        {
            c.LinkedRows = b.ReadBL();
            c.LinkedCols = b.ReadBL();
            b.ReadBL();
        }
        int ncc = b.ReadBL();
        c.HsNumContents = ncc;
        for (int i = 0; i < ncc; i++) ReadCellContent(b, c, primary: i == 0);
        c.CellStyle = ReadCellStyle(b);
        b.ReadBL();
        if (b.ReadBL() != 0)
        {
            c.HsHasGeomData = true;
            var gd = new Records.TableGeometryCellValues();
            gd.GeomDataFlag = b.ReadBL();
            gd.WidthWGap = b.ReadBD();
            gd.HeightWGap = b.ReadBD();
            int ng = b.ReadBL();
            gd.NumGeometry = ng;
            for (int i = 0; i < ng; i++)
            {
                var g = new Records.TableGeometryGeomValues();
                var tl = b.Read3BD(); g.TopLeftX = tl.X; g.TopLeftY = tl.Y; g.TopLeftZ = tl.Z;
                var ce = b.Read3BD(); g.CenterX = ce.X; g.CenterY = ce.Y; g.CenterZ = ce.Z;
                g.ContentWidth = b.ReadBD();
                g.ContentHeight = b.ReadBD();
                g.Width = b.ReadBD();
                g.Height = b.ReadBD();
                g.Unknown = b.ReadBL();
                gd.Geometry.Add(g);
            }
            c.GeomData = gd;
        }
        c.CellEndBit = b.BitPosition;
        return c;
    }
}
