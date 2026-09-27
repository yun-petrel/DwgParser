using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static void AssociateTableStyleRows(WalkResult result)
    {
        Dictionary<ulong, DwgObjectRecord>? records = null;
        foreach (var e in result.Entities)
        {
            if (e is not TableEntity table) continue;
            if (table.RowStyles.Count > 0 || table.BaseCellStyle is not null) continue;

            records ??= IndexRecordsByHandle(result);

            var styleHandle = table.TableStyleHandle;
            if (styleHandle == 0)
                styleHandle = TryReadThinTableStyle(table, records);
            if (styleHandle == 0) continue;
            if (!records.TryGetValue(styleHandle, out var styleRec)
                || styleRec.TypeCode != TableStyleRecord.TypeCodeValue
                || styleRec.TableStyle is not { Captured: true } cap)
                continue;

            table.TableStyleHandle = styleHandle;
            bool titleSuppressed = table.StyleOverride?.TitleSuppressed == true;
            for (int r = 0; r < table.TableRows; r++)
            {
                int rowType = r == 0 ? (titleSuppressed ? 2 : 1) : r == 1 ? 2 : 0;
                table.RowStyles.Add(new TableCellStyle
                {
                    DataFlags = 1,
                    ContentFormat = new TableContentFormat { TextStyleHandle = cap.Tsty[rowType] },
                });
            }
        }
    }

    private static ulong TryReadThinTableStyle(TableEntity table, Dictionary<ulong, DwgObjectRecord> records)
    {
        if (!records.TryGetValue(table.Handle, out var rec) || rec.Table is not { } cap) return 0;
        var axis = cap.TypeAxisRefs;
        if (axis.Count < 2) return 0;
        ulong h = axis[1].Resolve(table.Handle);
        return h != 0 && records.TryGetValue(h, out var target)
             && target.TypeCode == TableStyleRecord.TypeCodeValue ? h : 0;
    }

    private static Dictionary<ulong, DwgObjectRecord> IndexRecordsByHandle(WalkResult result)
    {
        var map = new Dictionary<ulong, DwgObjectRecord>();
        foreach (var rec in result.Records)
            map.TryAdd(rec.Handle, rec);
        return map;
    }
}
