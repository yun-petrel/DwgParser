using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class LayerCarryPreR2007Up
{
    public string Name => "AC1015→R2007+";

    public bool IsCaptured(DwgObjectRecord rec) => rec.Layer?.HasName == true
        && rec.Layer.LayerColor.HasValue;

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.Layer!;
        if (!t.UnicodeStrings || rec.SourceVersion >= DwgVersion.AC1021) return false;
        bool carriesRl = !t.R2010Plus;
        long start = rec.CarryStartBit;
        long ownStart = start + 32;
        if (from != (carriesRl ? start : ownStart)) return false;
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, ownStart);
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        long eedEnd = br.BitPosition;
        br.ReadBL();
        long nameStart = br.BitPosition;
        string name = br.ReadTV();
        if (name.Length == 0 || name != cap.Name) return false;
        bool xrefRef = br.ReadB();
        int resolved = br.ReadBS();
        bool xrefDep = br.ReadB();
        if (resolved != cap.XrefResolved) return false;
        int flag0 = br.ReadBS();
        if (flag0 is < 0 or > 0x7FFF) return false;
        if (((flag0 & 1) != 0) != cap.Frozen || ((flag0 & 2) != 0) != cap.Off
            || ((flag0 & 4) != 0) != cap.FrozenInNew || ((flag0 & 8) != 0) != cap.Locked
            || (((flag0 & 16) != 0) != (cap.PlotFlag ?? false))
            || ((flag0 & 0x3E0) >> 5) != (cap.LineWeightRaw ?? -1))
            return false;
        int colorIdx = br.ReadBS();
        if (colorIdx != cap.ColorIndex) return false;
        if (br.BitPosition != rec.HandleStreamStartBit) return false;

        var eedUp = new BitWriter(16);
        bool eedReencoded = TryCarryEedR2007Plus(eedUp, src, eedStart, eedEnd, rec);

        if (carriesRl) body.WriteRL(0);
        CopyBits(body, src, ownStart, (int)(eedStart - ownStart));
        if (eedReencoded) Append(body, eedUp);
        else CopyBits(body, src, eedStart, (int)(eedEnd - eedStart));
        CopyBits(body, src, eedEnd, (int)(nameStart - eedEnd));
        body.WriteB(SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8)
            || cap.DefaultMaterialHandle != 0);
        if (t.R2013Plus) body.WriteB(false);
        body.WriteBS((short)resolved);
        body.WriteBS((short)flag0);
        uint method = colorIdx == 256 ? 0xC0u : colorIdx == 0 ? 0xC1u : 0xC3u;
        uint rgb = (method << 24) | (method == 0xC3 ? (uint)colorIdx : 0u);
        body.WriteBS(0);
        body.WriteBL(unchecked((int)rgb));
        body.WriteRC(0);

        long contentStart = body.BitPosition;
        WriteCentralTu(body, name);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
        int dataBits = carriesRl ? (int)body.BitPosition - 32 : (int)body.BitPosition;
        if (carriesRl) body.PatchRL(0, (uint)(typeBits + 32 + dataBits));
        return true;
    }
}
