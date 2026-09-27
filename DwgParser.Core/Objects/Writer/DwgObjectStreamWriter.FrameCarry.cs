using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static (Records.MLineStyleCarryPlan?, Records.ViewStyleCarryPlan?,
        Records.StyleCarryPreR2007Up?, Records.TableStyleCarryAc1018ToAc1021?,
        Records.LtypeCarryPreR2007Up?, Records.LayerCarryPreR2007Up?,
        Records.XrecordCarryPreR2007Up?, Records.DimStyleCarryAcR2000Up?) FrameCarryPlansOf(
            DwgObjectRecord rec, Target t, LtypeTable? ltypeTable)
    {
        Records.MLineStyleCarryPlan? mlsPlan =
            rec.LogicalType == TypeMLineStyle && ltypeTable != null
                ? ObjectFactory.CreateMLineStyleCarry(rec, t)
                : null;
        Records.ViewStyleCarryPlan? vsPlan =
            (rec.LogicalType == TypeSectionViewStyle || rec.LogicalType == TypeDetailViewStyle)
                ? ObjectFactory.CreateViewStyleCarry(rec, t)
                : null;
        Records.StyleCarryPreR2007Up? styAc15Plan =
            rec.LogicalType == TypeStyle && t.Version == DwgVersion.AC1021
            && rec.SourceVersion == DwgVersion.AC1015
                ? ObjectFactory.CreateStyleCarry(rec, t) as Records.StyleCarryPreR2007Up
                : null;
        Records.TableStyleCarryAc1018ToAc1021? tsAc15Plan =
            rec.LogicalType == TypeTableStyle && t.Version == DwgVersion.AC1021
            && rec.SourceVersion == DwgVersion.AC1015
                ? ObjectFactory.CreateTableStyleCarry(rec, t) as Records.TableStyleCarryAc1018ToAc1021
                : null;
        Records.LtypeCarryPreR2007Up? ltAc15Plan =
            rec.LogicalType == TypeLType && t.Version == DwgVersion.AC1021
            && rec.SourceVersion == DwgVersion.AC1015
                ? ObjectFactory.CreateLtypeCarry(rec, t) as Records.LtypeCarryPreR2007Up
                : null;
        Records.LayerCarryPreR2007Up? lyAc15Plan =
            rec.LogicalType == TypeLayer && (t.Version == DwgVersion.AC1021 || t.R2010Plus)
            && rec.SourceVersion == DwgVersion.AC1015
                ? new Records.LayerCarryPreR2007Up()
                : null;
        Records.XrecordCarryPreR2007Up? xrAc15Plan =
            rec.LogicalType == 0x4F && t.Version == DwgVersion.AC1021
            && (rec.SourceVersion == DwgVersion.AC1015 || rec.SourceVersion == DwgVersion.AC1018)
                ? new Records.XrecordCarryPreR2007Up()
                : null;
        Records.DimStyleCarryAcR2000Up? dsAc15Plan =
            rec.LogicalType == TypeDimStyle
            && (t.Version == DwgVersion.AC1021 || t.R2010Plus)
            && rec.SourceVersion == DwgVersion.AC1015
                ? (t.R2010Plus
                    ? new Records.DimStyleCarryAcR2000ToR2010()
                    : new Records.DimStyleCarryAcR2000ToAc1021())
                : null;
        return (mlsPlan, vsPlan, styAc15Plan, tsAc15Plan, ltAc15Plan, lyAc15Plan, xrAc15Plan,
            dsAc15Plan);
    }
}
