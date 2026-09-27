using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class TableContentValueRecord
{
    private const int MaxTotalCells = 200_000;

    private readonly DwgObjectRecord _rec;
    private readonly int _objStart;
    private readonly BitReader _br;

    protected TableContentValueRecord(DwgObjectRecord rec, int objStart, BitReader br)
    {
        _rec = rec;
        _objStart = objStart;
        _br = br;
        Cap = rec.TableContentValue ??= new TableContentValueCapture();
        Cap.AttachIdentity(rec);
    }

    public TableContentValueCapture Cap { get; }

    public abstract string Family { get; }

    protected virtual bool HasDsData => false;

    public void Read()
    {
        var br = _br;
        DwgObjectRecord.SeekTo(br, _rec.CarryStartBit);
        if (!CentralStrings.TryDataBits(_rec, _objStart, br, out uint bitsize, out _))
        { Fail("集中流 bitsize 未定位"); return; }
        if (!CentralStrings.TryMeasure(_rec, br.UnderlyingData(), _objStart, bitsize,
                out BitReader str, out long areaStart, out long areaEnd))
        { Fail("集中流串区量不出"); return; }

        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail("EED"); return; }
        if (br.ReadBL() is < 0 or > 100_000) { Fail("num_reactors"); return; }
        br.ReadB();
        if (HasDsData) br.ReadB();

        var v = new TableContentValues { Layout = Family };
        var savedStrings = br.StringStream;
        br.StringStream = str;
        string at = "ldata.name";
        int cells = 0;
        try
        {
            v.LdataName = br.ReadTV();
            at = "ldata.description";
            v.Description = br.ReadTV();

            at = "num_cols";
            v.NumCols = Count(br, 5000);
            for (int i = 0; i < v.NumCols; i++)
            {
                var col = new TableContentColumnValues();
                at = $"col[{i}].name";
                col.Name = br.ReadTV();
                at = $"col[{i}].custom_data";
                col.CustomData = br.ReadBL();
                at = $"col[{i}].num_items";
                col.NumCustomDataItems = Count(br, 10_000);
                for (int j = 0; j < col.NumCustomDataItems; j++)
                {
                    at = $"col[{i}].item[{j}]";
                    col.HsCustomDataRefs += TableAcR2010Plus.SkipCustomData(br);
                }
                at = $"col[{i}].cellstyle";
                col.Style = TableAcR2010Plus.ReadCellStyle(br);
                at = $"col[{i}].style_id";
                col.StyleId = br.ReadBL();
                at = $"col[{i}].width";
                col.Width = br.ReadBD();
                v.Columns.Add(col);
            }

            at = "num_rows";
            v.NumRows = Count(br, 5000);
            for (int i = 0; i < v.NumRows; i++)
            {
                var row = new TableContentRowValues();
                at = $"row[{i}].num_cells";
                row.NumCells = Count(br, MaxTotalCells);
                cells += row.NumCells;
                if (cells > MaxTotalCells) throw new InvalidDataException("格总数越预算");
                at = $"row[{i}].cells";
                for (int j = 0; j < row.NumCells; j++) row.Cells.Add(TableAcR2010Plus.ReadCell(br));
                at = $"row[{i}].custom_data";
                row.CustomData = br.ReadBL();
                at = $"row[{i}].num_items";
                row.NumCustomDataItems = Count(br, 10_000);
                for (int j = 0; j < row.NumCustomDataItems; j++)
                {
                    at = $"row[{i}].item[{j}]";
                    row.HsCustomDataRefs += TableAcR2010Plus.SkipCustomData(br);
                }
                at = $"row[{i}].cellstyle";
                row.Style = TableAcR2010Plus.ReadCellStyle(br);
                at = $"row[{i}].style_id";
                row.StyleId = br.ReadBL();
                at = $"row[{i}].height";
                row.Height = br.ReadBD();
                v.Rows.Add(row);
            }

            at = "num_field_refs";
            v.NumFieldRefs = Count(br, 10_000);
            at = "base cellstyle";
            v.BaseStyle = TableAcR2010Plus.ReadCellStyle(br);
            at = "num_merged";
            int numMerged = Count(br, 100_000);
            for (int i = 0; i < numMerged; i++)
            {
                at = $"merged[{i}]";
                v.MergedCells.Add(new TableMergedCell(br.ReadBL(), br.ReadBL(), br.ReadBL(), br.ReadBL()));
            }
        }
        catch (Exception ex)
        {
            br.StringStream = savedStrings;
            Fail($"531 厚形@{at}:{ex.GetType().Name}");
            return;
        }
        br.StringStream = savedStrings;

        if (br.BitPosition != areaStart)
        { Fail($"数值区末不落串区起点(Δ={br.BitPosition - areaStart})"); return; }
        long used = str.BitPosition - areaStart;
        if (used < 0 || used > areaEnd - areaStart)
        { Fail($"集中流消费越界(used={used} area={areaEnd - areaStart})"); return; }

        v.StringsConsumedBits = used;
        Cap.FailReason = "";
        Cap.WalkEndBit = br.BitPosition;
        Cap.ValuesCommitted = true;
        CapValues(v);
    }

    private void CapValues(TableContentValues v)
    {
        var t = Cap.Values;
        t.Layout = v.Layout;
        t.LdataName = v.LdataName;
        t.Description = v.Description;
        t.NumCols = v.NumCols;
        t.NumRows = v.NumRows;
        t.NumFieldRefs = v.NumFieldRefs;
        t.BaseStyle = v.BaseStyle;
        t.Columns.AddRange(v.Columns);
        t.Rows.AddRange(v.Rows);
        t.MergedCells.AddRange(v.MergedCells);
        t.StringsConsumedBits = v.StringsConsumedBits;
    }

    private static int Count(BitReader br, int max)
    {
        int n = br.ReadBL();
        if (n < 0 || n > max) throw new InvalidDataException($"计数越界 {n}>{max}");
        return n;
    }

    private void Fail(string why)
    {
        Cap.FailReason = why;
        Cap.WalkEndBit = _br.BitPosition;
    }
}

internal sealed class TableContentValueAcR2007 : TableContentValueRecord
{
    public TableContentValueAcR2007(DwgObjectRecord rec, int objStart, BitReader br)
        : base(rec, objStart, br) { }

    public override string Family => "TABLECONTENT(R2007+)";
}

internal sealed class TableContentValueAcR2010 : TableContentValueRecord
{
    public TableContentValueAcR2010(DwgObjectRecord rec, int objStart, BitReader br)
        : base(rec, objStart, br) { }

    public override string Family => "TABLECONTENT(R2007+)";
}

internal sealed class TableContentValueAcR2013 : TableContentValueRecord
{
    public TableContentValueAcR2013(DwgObjectRecord rec, int objStart, BitReader br)
        : base(rec, objStart, br) { }

    public override string Family => "TABLECONTENT(R2007+)";

    protected override bool HasDsData => true;
}

internal sealed class TableContentValueAcR2018 : TableContentValueRecord
{
    public TableContentValueAcR2018(DwgObjectRecord rec, int objStart, BitReader br)
        : base(rec, objStart, br) { }

    public override string Family => "TABLECONTENT(R2007+)";

    protected override bool HasDsData => true;
}
