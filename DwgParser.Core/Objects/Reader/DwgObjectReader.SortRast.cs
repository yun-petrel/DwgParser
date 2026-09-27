using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const short TypeSortentsTable = 517;
    private const short TypeRasterVariables = 542;
    private const short TypeSun = 552;
    private const short TypeFieldList = 527;
    private const short TypeField = 568;
    private const short TypePdfDefinition = 544;

    private static void TryDecodeSortRast(BitReader br, ulong handle, DwgVersion version,
        DwgObjectRecord? record, int objStart, short logical)
    {
        if (record == null) return;
        long savedBit = br.BitPosition;
        DwgObjectRecord.SeekTo(br, record.CarryStartBit);
        try
        {
            switch (logical)
            {
                case TypeSortentsTable:
                {
                    var st = ObjectFactory.CreateSortentsTable(handle, version, record, objStart);
                    if (st != null) ObjectFactory.Decode(st, br);
                    break;
                }
                case TypeRasterVariables:
                {
                    var rv = ObjectFactory.CreateRasterVariables(handle, version, record, objStart);
                    if (rv != null) ObjectFactory.Decode(rv, br);
                    break;
                }
                case TypeSun:
                {
                    var sun = ObjectFactory.CreateSun(handle, version, record, objStart);
                    if (sun != null) ObjectFactory.Decode(sun, br);
                    break;
                }
                case TypeFieldList:
                {
                    var fl = ObjectFactory.CreateFieldList(handle, version, record, objStart);
                    if (fl != null) ObjectFactory.Decode(fl, br);
                    break;
                }
                case TypePdfDefinition:
                {
                    var ud = ObjectFactory.CreateUnderlayDefinition(handle, version, record, objStart);
                    if (ud != null) ObjectFactory.Decode(ud, br);
                    break;
                }
                case TypeField:
                {
                    var fd = ObjectFactory.CreateField(handle, version, record, objStart);
                    if (fd != null) ObjectFactory.Decode(fd, br);
                    else if (record != null)
                    {
                        var cap = record.Field ??= new Records.FieldCapture();
                        cap.AttachIdentity(record);
                        if (!cap.Captured && string.IsNullOrEmpty(cap.FailReason))
                        {
                            cap.FailReason = "契约:R2007+ A槽/[4][8][2]槽与求值链语义未名(§605 §三),第二批落";
                            cap.FailBit = br.BitPosition - record.CarryStartBit;
                            cap.WalkEndBit = br.BitPosition;
                        }
                    }
                    break;
                }
                case TypeTableGeometry:
                {
                    var tg = ObjectFactory.CreateTableGeometry(handle, version, record, objStart);
                    if (tg != null) ObjectFactory.Decode(tg, br);
                    else if (record != null)
                    {
                        var cap = record.TableGeometry ??= new Records.TableGeometryCapture();
                        cap.AttachIdentity(record);
                        if (!cap.Captured && string.IsNullOrEmpty(cap.FailReason))
                        {
                            cap.FailReason = "契约:532 类 R2004+ 才有(AC1015 档=降级非合规构造,批次 52/53)";
                            cap.WalkEndBit = br.BitPosition;
                        }
                    }
                    break;
                }
            }
        }
        catch {  }
        br.SetPosition((int)(savedBit >> 3), (int)(savedBit & 7));
    }
}
