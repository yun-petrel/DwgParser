using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool FinalizeObjectFrame(BitWriter main, BitWriter body, BitWriter handles,
        int handlesBitLen, BitWriter typeW, int typeBits, bool bodyCarriesRl,
        DwgObjectRecord rec, byte[] src, Target t, bool reencode,
        long cfDataEnd, long derivedHsBit, bool handlesSplitFromSource)
    {
        bool vpRewritten = rec.LogicalType == TypeViewport
            && rec.SourceVersion is DwgVersion.AC1015 or DwgVersion.AC1018
            && t.R2007Plus;
        bool synthNoStream = rec.HasSyntheticFrame && !rec.WroteCentralStringStream;
        if (t.R2007Plus
            && (rec.SourceVersion < DwgVersion.AC1021 || synthNoStream)
            && rec.StringFields.Count == 0
            && !rec.WroteCentralStringStream
            && rec.LogicalType != 499
            && !vpRewritten)
        {
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"STYTAIL0 h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} SF={rec.StringFields.Count} 补 has_strings=0");
            body.WriteB(false);
        }


        int aBits = (int)body.BitPosition;

        int bBits = handlesBitLen >= 0 ? handlesBitLen : (int)handles.BitPosition;

        bool bodyIsDataOnly = derivedHsBit > 0 || cfDataEnd > 0
            || (handlesSplitFromSource && rec.SourceVersion != t.Version);
        if (bodyCarriesRl && bodyIsDataOnly)
            body.PatchRL(0, (uint)(typeBits + aBits));

        if (!t.R2010Plus)
        {
            int rlBits = bodyCarriesRl ? 0 : 32;
            int bitsize = bodyCarriesRl ? 0 : (typeBits + 32 + aBits);
            long totalBits = typeBits + rlBits + aBits + bBits;
            uint size = (uint)((totalBits + 7) / 8);

            main.AlignByte();
            int msStart = main.BytePosition;
            main.WriteMS(size);
            Append(main, typeW);
            if (!bodyCarriesRl)
                main.WriteRL((uint)bitsize);
            Append(main, body);
            Append(main, handles);
            PadToByte(main, totalBits, size, rec, src, t, reencode);
            WriteFrameCrc(main, msStart, rec, src, t, reencode);
            return true;
        }
        else
        {
            int p = typeBits + aBits;
            int q = p + bBits;
            int pad = (8 - (q % 8)) % 8;
            uint size = (uint)((q + pad) / 8);
            uint hs = (uint)(bBits + pad);
            if (size == 0) return false;

            main.AlignByte();
            int msStart = main.BytePosition;
            main.WriteMS(size);
            main.WriteUMC(hs);
            Append(main, typeW);
            Append(main, body);
            Append(main, handles);
            main.SkipBits(pad);
            WriteFrameCrc(main, msStart, rec, src, t, reencode);
            return true;
        }
    }
}
