using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

internal static class AssocEvalVariantTable
{
    internal static AssocEvalVariantKind KindOf(int gc)
    {
        if (gc == 0) return AssocEvalVariantKind.Null;
        if (gc < 0) return AssocEvalVariantKind.Handle;
        if (gc < 5) return AssocEvalVariantKind.Text;
        if (gc == 5) return AssocEvalVariantKind.Handle;
        if (gc <= 9) return AssocEvalVariantKind.Text;
        if (gc < 38) return AssocEvalVariantKind.Unknown;
        if (gc <= 59) return AssocEvalVariantKind.Real;
        if (gc <= 79) return AssocEvalVariantKind.Int16;
        if (gc <= 99) return AssocEvalVariantKind.Int32;
        if (gc <= 102) return AssocEvalVariantKind.Text;
        if (gc == 105) return AssocEvalVariantKind.Handle;
        if (gc < 140) return AssocEvalVariantKind.Unknown;
        if (gc <= 149) return AssocEvalVariantKind.Real;
        if (gc <= 169) return AssocEvalVariantKind.Unknown;
        if (gc <= 179) return AssocEvalVariantKind.Int16;
        if (gc < 210) return AssocEvalVariantKind.Unknown;
        if (gc <= 269) return AssocEvalVariantKind.Unknown;
        if (gc <= 279) return AssocEvalVariantKind.Int16;
        if (gc <= 289) return AssocEvalVariantKind.Int8;
        if (gc < 300) return AssocEvalVariantKind.Unknown;
        if (gc <= 309) return AssocEvalVariantKind.Text;
        if (gc <= 319) return AssocEvalVariantKind.Unknown;
        if (gc <= 329) return AssocEvalVariantKind.Handle;
        if (gc <= 369) return AssocEvalVariantKind.Unknown;
        if (gc <= 389) return AssocEvalVariantKind.Int16;
        if (gc <= 399) return AssocEvalVariantKind.Handle;
        if (gc <= 409) return AssocEvalVariantKind.Int16;
        if (gc <= 419) return AssocEvalVariantKind.Text;
        if (gc <= 429) return AssocEvalVariantKind.Int32;
        if (gc < 440) return AssocEvalVariantKind.Text;
        if (gc <= 459) return AssocEvalVariantKind.Int32;
        if (gc <= 469) return AssocEvalVariantKind.Real;
        if (gc <= 479) return AssocEvalVariantKind.Text;
        if (gc < 999) return AssocEvalVariantKind.Unknown;
        if (gc < 1000) return AssocEvalVariantKind.Text;
        if (gc == 1004) return AssocEvalVariantKind.Unknown;
        if (gc <= 1009) return AssocEvalVariantKind.Text;
        if (gc <= 1039) return AssocEvalVariantKind.Unknown;
        if (gc <= 1042) return AssocEvalVariantKind.Real;
        if (gc <= 1069) return AssocEvalVariantKind.Unknown;
        if (gc <= 1070) return AssocEvalVariantKind.Int16;
        if (gc == 1071) return AssocEvalVariantKind.Int32;
        return AssocEvalVariantKind.Unknown;
    }
}
