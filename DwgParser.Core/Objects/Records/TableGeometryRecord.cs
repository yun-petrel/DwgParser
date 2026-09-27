using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class TableGeometryRecord : ITableGeometry
{
    private const double MaxReasonableDouble = 1e12;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected TableGeometryRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.TableGeometry ??= new TableGeometryCapture();
        Cap.AttachIdentity(rec);
    }

    public TableGeometryCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public long NumRows => Cap.Values.NumRows;
    public long NumCols => Cap.Values.NumCols;
    public IReadOnlyList<TableGeometryCellValues> Cells => Cap.Values.Cells;
    public bool StringStreamPresent => Cap.Values.HasStrings == 1;
    public bool Captured => Cap.Captured;

    protected virtual bool HasInlineRl => true;
    protected virtual bool HasXdicMissing => true;
    protected virtual bool HasDsData => false;
    protected virtual bool HasTrailingStrings => false;

    internal virtual void Read(BitReader br)
    {
        var v = Cap.Values;
        v.Layout = "TABLEGEOMETRY(R2004+)";
        long hs;
        if (HasInlineRl)
        {
            uint rl = br.ReadRL();
            hs = (long)ObjStart * 8 + rl;
        }
        else
        {
            hs = Rec.HandleStreamStartBit;
        }
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界"); return; }

        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return; }
        if (br.ReadBL() is < 0 or > 100_000) { Fail(br, "num_reactors"); return; }
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();

        string at = "numrows";
        try
        {
            v.NumRows = Bl(br, 5000);
            at = "numcols";
            v.NumCols = Bl(br, 5000);
            at = "num_cells";
            v.NumCells = Bl(br, 10000);
            for (var i = 0; i < v.NumCells; i++)
            {
                var cell = new TableGeometryCellValues();
                at = $"cell[{i}].geom_data_flag";
                cell.GeomDataFlag = br.ReadBL();
                at = $"cell[{i}].width_w_gap";
                cell.WidthWGap = Bd(br);
                at = $"cell[{i}].height_w_gap";
                cell.HeightWGap = Bd(br);
                at = $"cell[{i}].num_geometry";
                cell.NumGeometry = Bl(br, 10000);
                for (var g = 0; g < cell.NumGeometry; g++)
                {
                    var geom = new TableGeometryGeomValues();
                    at = $"cell[{i}].geom[{g}].dist_top_left";
                    (geom.TopLeftX, geom.TopLeftY, geom.TopLeftZ) = (Bd(br), Bd(br), Bd(br));
                    at = $"cell[{i}].geom[{g}].dist_center";
                    (geom.CenterX, geom.CenterY, geom.CenterZ) = (Bd(br), Bd(br), Bd(br));
                    at = $"cell[{i}].geom[{g}].content_width";
                    geom.ContentWidth = Bd(br);
                    at = $"cell[{i}].geom[{g}].content_height";
                    geom.ContentHeight = Bd(br);
                    at = $"cell[{i}].geom[{g}].width";
                    geom.Width = Bd(br);
                    at = $"cell[{i}].geom[{g}].height";
                    geom.Height = Bd(br);
                    at = $"cell[{i}].geom[{g}].unknown";
                    geom.Unknown = br.ReadBL();
                    cell.Geometry.Add(geom);
                }
                v.Cells.Add(cell);
            }
            if (HasTrailingStrings) v.HasStrings = br.ReadB() ? 1 : 0;
        }
        catch (Exception ex)
        {
            Fail(br, $"532 走查@{at}:{ex.GetType().Name}");
            return;
        }

        if (br.BitPosition != hs) { Fail(br, $"不闭合(游标未落 RL 锚)(Δ={br.BitPosition - hs})"); return; }

        Cap.WalkEndBit = br.BitPosition;
        Cap.FailReason = "";
        Cap.Captured = true;
    }

    private static int Bl(BitReader br, int max)
    {
        int n = br.ReadBL();
        if (n < 0 || n > max) throw new InvalidDataException($"BL 越界 {n}>{max}");
        return n;
    }

    private static double Bd(BitReader br)
    {
        double d = br.ReadBD();
        if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > MaxReasonableDouble)
            throw new InvalidDataException("BD 非法");
        return d;
    }

    private void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.WalkEndBit = br.BitPosition;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }
}
