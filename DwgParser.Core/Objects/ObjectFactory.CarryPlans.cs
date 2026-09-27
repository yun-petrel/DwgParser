using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class ObjectFactory
{

    internal static DimStyleCarryPlan? CreateDimStyleCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (t.UnicodeStrings && rec.SourceVersion == DwgVersion.AC1018)
            return t.R2010Plus ? new DimStyleCarryAc1018ToR2010() : new DimStyleCarryAc1018ToAc1021();
        if (t.R2010Plus && rec.SourceVersion == DwgVersion.AC1021)
            return new DimStyleCarryAc1021Up();
        if (rec.SourceVersion.IsR2010Plus() && t.Version == DwgVersion.AC1021)
            return new DimStyleCarryR2010ToAc1021();
        return null;
    }

    internal static VisualStyleCarryPlan? CreateVisualStyleCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (t.R2010Plus && rec.SourceVersion == DwgVersion.AC1021)
            return new VisualStyleCarryAc1021Up();
        if (rec.SourceVersion.IsR2010Plus() && t.Version == DwgVersion.AC1021)
            return new VisualStyleCarryAcR2010Down();
        if (rec.SourceVersion == DwgVersion.AC1018 && t.Version == DwgVersion.AC1021)
            return new VisualStyleCarryAc1018ToAc1021();
        return null;
    }

    internal static StyleCarryPlan? CreateStyleCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (t.UnicodeStrings && rec.SourceVersion >= DwgVersion.AC1015
            && rec.SourceVersion < DwgVersion.AC1021)
            return new StyleCarryPreR2007Up();
        return null;
    }

    internal static LtypeCarryPlan? CreateLtypeCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (t.UnicodeStrings && rec.SourceVersion >= DwgVersion.AC1015
            && rec.SourceVersion < DwgVersion.AC1021)
            return new LtypeCarryPreR2007Up();
        return null;
    }

    internal static TableStyleCarryPlan? CreateTableStyleCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (t.R2010Plus && t.UnicodeStrings && rec.SourceVersion >= DwgVersion.AC1015
            && rec.SourceVersion <= DwgVersion.AC1021)
            return new TableStyleCarryR2007ToR2010();
        if (t.Version == DwgVersion.AC1021 && t.UnicodeStrings && rec.SourceVersion >= DwgVersion.AC1024)
            return new TableStyleCarryR2010ToAc1021();
        if (t.Version == DwgVersion.AC1021 && t.UnicodeStrings
            && (rec.SourceVersion == DwgVersion.AC1015 || rec.SourceVersion == DwgVersion.AC1018))
            return new TableStyleCarryAc1018ToAc1021();
        if ((t.Version == DwgVersion.AC1018 && rec.SourceVersion >= DwgVersion.AC1021)
            || (t.Version == DwgVersion.AC1015 && rec.SourceVersion >= DwgVersion.AC1021))
            return new TableStyleCarryR2010ToAc1018();
        return null;
    }

    internal static MLeaderStyleCarryPlan? CreateMLeaderStyleCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (t.R2010Plus && rec.SourceVersion == DwgVersion.AC1021)
            return new MLeaderStyleCarryAc1021Up();
        if (rec.SourceVersion.IsR2010Plus() && t.Version == DwgVersion.AC1021)
            return new MLeaderStyleCarryAcR2010Down();
        if (rec.SourceVersion == DwgVersion.AC1018 && t.Version == DwgVersion.AC1021)
            return new MLeaderStyleCarryAc1018ToAc1021();
        return null;
    }

    internal static MaterialCarryPlan? CreateMaterialCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (rec.SourceVersion is DwgVersion.AC1018 or DwgVersion.AC1015
            && (t.Version == DwgVersion.AC1021 || t.R2010Plus))
            return new MaterialCarryAc1018ToAc1021();
        return null;
    }

    internal static MLineStyleCarryPlan? CreateMLineStyleCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if ((rec.SourceVersion.IsR2010Plus() || rec.SourceVersion == DwgVersion.AC1021)
            && (t.R2010Plus || t.Version == DwgVersion.AC1021)
            && (rec.SourceVersion >= DwgVersion.AC1032) != (t.Version >= DwgVersion.AC1032)
            && rec.SourceVersion != t.Version)
            return new MLineStyleCarryAcR2018Cross();
        return null;
    }

    internal static ViewStyleCarryPlan? CreateViewStyleCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (rec.SourceVersion.IsR2010Plus()
            && (t.R2010Plus || t.Version == DwgVersion.AC1021)
            && rec.SourceVersion >= DwgVersion.AC1032
            && rec.SourceVersion != t.Version)
            return new ViewStyleCarryAcR2018Down();
        if (t.Version == DwgVersion.AC1018
            && rec.SourceVersion >= DwgVersion.AC1021
            && rec.SourceVersion != t.Version)
            return rec.LogicalType == SectionViewStyleCarryToAc1018.TypeSectionViewStyle
                ? new SectionViewStyleCarryToAc1018()
                : new ViewStyleCarryToAc1018();
        if ((t.R2010Plus || t.Version == DwgVersion.AC1021) && t.UnicodeStrings
            && (rec.LogicalType == 510 || rec.LogicalType == 511))
        {
            if (rec.SourceVersion == DwgVersion.AC1015)
                return new ViewStylePreR2004CentralizeToR2010();
            if (rec.SourceVersion == DwgVersion.AC1018)
                return new ViewStyleCentralizeToR2010();
        }
        return null;
    }
}
