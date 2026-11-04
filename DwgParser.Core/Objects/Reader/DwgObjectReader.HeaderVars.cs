using System.Globalization;
using DwgParser.Core.Format;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    public sealed partial class WalkResult
    {
        public double Ltscale { get; set; } = 1.0;

        public double Celtscale { get; set; } = 1.0;
    }

    public static void FillLinetypeScales(WalkResult result, IReadOnlyList<DwgSection> sections, DwgVersion version)
    {
        var header = sections.FirstOrDefault(s => s.Name == "Header" && s.Data is { Length: > 32 });
        if (header is null) return;
        var headerData = header.Data;
        if (headerData is null) return;
        try
        {
            var v = HeaderVarsCodec.Decode(headerData, version);
            result.Ltscale = PositiveOr1(v, "LTSCALE");
            result.Celtscale = PositiveOr1(v, "CELTSCALE");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException
                                      or OverflowException or KeyNotFoundException)
        {
        }
    }

    private static double PositiveOr1(Dictionary<string, object?> values, string name)
    {
        if (!values.TryGetValue(name, out var o) || o is null) return 1.0;
        try
        {
            double d = Convert.ToDouble(o, CultureInfo.InvariantCulture);
            return d > 0 ? d : 1.0;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return 1.0;
        }
    }
}
