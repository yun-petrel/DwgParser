using DwgParser.Core;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static class EntityHeadStringCapture
{
    internal static string Capture(BitReader br, DwgObjectRecord? record, DwgVersion version,
        ulong handle, int objStart, string dbgTag)
    {
        if (record != null && version >= DwgVersion.AC1021)
        {
            long nameGeoPos = br.BitPosition;
            string head = br.ReadTV();
            record.StringFields.Add(new DwgObjectRecord.StringField(nameGeoPos, 0, head));
            if (DwgObjectStreamWriter.DebugTrace && !string.IsNullOrEmpty(head))
                System.Console.Error.WriteLine(
                    $"DWGDBG {dbgTag} h={handle:X} @objStart={objStart} name=\"{head}\" geoPos={nameGeoPos}");
            return head;
        }
        else if (record != null && version.IsSupportedVersion())
        {
            long nameStart = br.BitPosition;
            string head = br.ReadTV();
            record.CaptureString(nameStart, br.BitPosition, head);
            if (DwgObjectStreamWriter.DebugTrace && !string.IsNullOrEmpty(head))
                System.Console.Error.WriteLine(
                    $"DWGDBG {dbgTag}INLINE h={handle:X} name=\"{head}\" bits[{nameStart},{br.BitPosition})");
            return head;
        }
        return "";
    }
}
