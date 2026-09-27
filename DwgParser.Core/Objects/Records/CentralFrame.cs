using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal static class CentralFrame
{
    public static bool TryRlFrame(DwgObjectRecord rec, int objStart, BitReader br,
        out long strAreaStart, out long dataEnd, out BitReader? strBr)
    {
        strAreaStart = 0;
        dataEnd = 0;
        strBr = null;
        uint bitsize = br.ReadRL();
        long end = (long)objStart * 8 + bitsize;
        if (end <= br.BitPosition || end > (long)rec.BodyEnd * 8) return false;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        dataEnd = end;

        var scratch = new DwgObjectRecord();
        var bridge = new BitReader(br.UnderlyingData(), 0) { UnicodeStrings = true };
        DwgObjectReader.AttachStringStream(bridge, objStart, bitsize, rec.SourceVersion, scratch);
        if (bridge.StringStream is null || scratch.StringStreamStartBit <= 0) return false;
        strAreaStart = scratch.StringStreamStartBit;
        strBr = bridge;
        return true;
    }

    public static bool TryBotFrame(DwgObjectRecord rec, BitReader br,
        out long strAreaStart, out long dataEnd, out BitReader? strBr)
    {
        dataEnd = rec.HandleStreamStartBit;
        strAreaStart = rec.StringStreamStartBit;
        strBr = br;
        if (dataEnd <= br.BitPosition || strAreaStart <= 0) return false;
        br.ReadHandle();
        return DwgObjectReader.TrySkipEed(br);
    }
}
