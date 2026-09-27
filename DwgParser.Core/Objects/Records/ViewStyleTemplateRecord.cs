using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class ViewStyleTemplateRecord : IViewStyleTemplate
{
    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    private const short TypeSectionViewStyle = 510;

    protected ViewStyleTemplateRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.ViewStyle ??= new ViewStyleCapture();
        Cap.AttachIdentity(rec);
    }

    public ViewStyleCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.TypeCode;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public bool TemplateCaptured => Cap.TemplateCaptured;

    protected abstract bool StringsCentralized { get; }

    internal virtual void Read(BitReader br)
    {
        bool is510 = Rec.LogicalType == TypeSectionViewStyle;
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return; }
        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return; }
        int numReactors = br.ReadBL();
        bool xdicMissing = false;
        if (Rec.SourceVersion >= DwgVersion.AC1018) xdicMissing = br.ReadB();
        if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return; }
        long headEnd = br.BitPosition;
        if (StringsCentralized)
        {
            DwgObjectReader.AttachStringStream(br, ObjStart, bitsize, Rec.SourceVersion, Rec);
            if (Rec.StringStreamStartBit <= 0) { Fail(br, "集中流起点未定位"); return; }
        }
        string desc, exclude = "", pattern = "", hatch = "";
        br.ReadBS();
        if (StringsCentralized)
        {
            DwgObjectRecord.SeekTo(br, Rec.StringStreamStartBit);
            desc = br.ReadTV();
            exclude = br.ReadTV();
            pattern = br.ReadTV();
            if (is510) hatch = br.ReadTV();
            if (desc.Length == 0 || desc.Length > 255) { Fail(br, $"串流 desc 长度={desc.Length}"); return; }
            DwgObjectRecord.SeekTo(br, headEnd);
        }
        else
        {
            desc = br.ReadTV();
            if (desc.Length == 0 || desc.Length > 255) { Fail(br, $"内联 desc 长度={desc.Length}"); return; }
        }
        if (!br.ReadB()) { Fail(br, "is_modified_for_recompute=0"); return; }
        int classVersion = br.ReadBS();
        if (classVersion != 0) { Fail(br, $"class_version={classVersion}"); return; }
        int flags = br.ReadBL();
        if (flags != (is510 ? 230 : 7)) { Fail(br, $"flags={flags}≠{(is510 ? 230 : 7)}"); return; }
        if (Rec.SourceVersion >= DwgVersion.AC1018)
        {
            if (!IsViewColor(DwgObjectReader.ReadCmcTriple(br), 0xC0000000))
            { Fail(br, "identifier_color 非真本常量"); return; }
        }
        else
        {
            int idx = br.ReadBS();
            if (idx != 256) { Fail(br, $"identifier_color 索引={idx}≠256"); return; }
        }
        double idHeight = br.ReadBD();
        if (idHeight != 5.0) { Fail(br, $"identifier_height={idHeight}≠5.0"); return; }
        long dataPos = br.BitPosition;
        if (dataPos >= hs) { Fail(br, "数据区余量不足"); return; }
        long hsOff = hs - dataPos;
        if (hsOff > 128)
        {
            for (long p = dataPos + 64; p < hs - 16; p += 2)
            {
                if (pattern.Length > 3 && (!is510 || (exclude.Length > 3 && hatch.Length > 3)))
                    break;
                try
                {
                    DwgObjectRecord.SeekTo(br, p);
                    string probe = br.ReadTV();
                    if (probe.Length < 3 || probe.Length > 256 || br.BitPosition > hs) continue;
                    if (pattern.Length < 3 && probe.Contains("AcVar"))
                    {
                        pattern = probe;
                    }
                    else if (is510 && exclude.Length < 3 && probe == "I, O, Q, S, X, Z")
                    {
                        exclude = probe;
                    }
                    else if (is510 && hatch.Length < 3 && probe == "ANSI31")
                    {
                        hatch = probe;
                    }
                }
                catch { }
            }
        }
        DwgObjectRecord.SeekTo(br, hs);
        var h0 = br.ReadHandle();
        if (h0.Code != 4) { Fail(br, $"句柄流 owner code={h0.Code}≠4"); return; }
        for (int i = 0; i < numReactors; i++) br.ReadHandle();
        if (!xdicMissing) br.ReadHandle();
        for (int i = 0; i < 6; i++)
        {
            var h = br.ReadHandle();
            if (h.Code != 5) { Fail(br, $"第 {i} 枚轴引用 code={h.Code}≠5"); return; }
        }
        long frameEnd = (long)Rec.BodyEnd * 8;
        for (long p = br.BitPosition; p < frameEnd; p++)
            if (br.ReadB()) { Fail(br, "句柄流尾段非全 0"); return; }
        Cap.TemplateCaptured = true;
        Cap.TemplateDesc = desc;
        Cap.TemplateExclude = exclude;
        Cap.TemplatePattern = pattern;
        Cap.TemplateHatchPattern = hatch;
        Cap.TemplateHeadEndBit = headEnd;
    }

    private void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }

    protected static bool IsViewColor((int Idx, uint Rgb, byte Flag) c, uint rgb)
        => c.Idx == 0 && c.Flag == 0 && c.Rgb == rgb;
}
