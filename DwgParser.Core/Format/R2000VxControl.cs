using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

internal static class R2000VxControl
{
    internal const short TypeCode = 0x46;

    internal static readonly byte[] Frame =
    {
        0x0B, 0x00,
        0x51, 0x92, 0x00, 0x00, 0x00, 0x00,
        0x80, 0x8C, 0xEA, 0x40, 0x30,
        0x83, 0x6E,
    };

    internal static bool FrameIsSelfConsistent()
        => Crc.Compute16Dwg(Frame, 0, Frame.Length - 2)
           == (ushort)(Frame[^2] | (Frame[^1] << 8));

    internal static bool TrySynthesisHandle(DwgObjectReader.WalkResult? walk, out ulong handle)
    {
        handle = 0;
        if (walk is { Records.Count: > 0 } w && !HasVxControl(w))
        {
            foreach (var r in w.Records)
                if (r.Handle > handle) handle = r.Handle;
            handle++;
            return true;
        }
        return false;
    }

    private static bool HasVxControl(DwgObjectReader.WalkResult walk)
    {
        foreach (var r in walk.Records)
            if (r.TypeCode == TypeCode) return true;
        return false;
    }
}
