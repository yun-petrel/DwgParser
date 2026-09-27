using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static void TryDecodeRenderSettings(BitReader br, ulong handle, DwgVersion version,
        DwgObjectRecord? record, int objStart)
    {
        if (record == null || version < DwgVersion.AC1015 || version > DwgVersion.AC1021) return;
        var rs = ObjectFactory.CreateRenderSettings(handle, version, record.ClassName, record, objStart);
        if (rs != null) ObjectFactory.Decode(rs, br);
    }
}
