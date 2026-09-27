using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static void DecodeDictWdflt(BitReader br, ulong handle,
        DwgVersion version, DwgObjectRecord? record, int objStart)
    {
        if (record == null) return;
        var dw = ObjectFactory.CreateDictWdflt(handle, version, record, objStart);
        if (dw != null) ObjectFactory.Decode(dw, br);
    }
}
