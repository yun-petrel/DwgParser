using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class UnderlayDefinitionRecord : IUnderlayDefinition
{
    internal const int MaxPathChars = 8_192;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected UnderlayDefinitionRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.UnderlayDefinition ??= new UnderlayDefinitionCapture();
        Cap.AttachIdentity(rec);
        Cap.UnderlayKind = KindOf(rec.ClassName);
    }

    public UnderlayDefinitionCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Filename { get; protected set; } = "";
    public string Name { get; protected set; } = "";
    public string UnderlayKind => Cap.UnderlayKind;
    public bool Captured => Cap.Captured;

    internal static string KindOf(string? className)
    {
        var n = (className ?? "").ToUpperInvariant();
        if (n.Contains("PDF")) return "PDF";
        if (n.Contains("DGN")) return "DGN";
        if (n.Contains("DWF")) return "DWF";
        return "";
    }

    protected virtual bool HasInlineRl => true;
    protected virtual bool HasXdicMissing => false;
    protected virtual bool HasDsData => false;
    protected virtual bool StringsCentralized => false;

    internal virtual void Read(BitReader br)
    {
        long hs;
        if (HasInlineRl)
        {
            uint rl = br.ReadRL();
            hs = (long)ObjStart * 8 + rl;
        }
        else
        {
            hs = Rec.HandleStreamStartBit;
        }
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail("帧界"); return; }
        if (StringsCentralized) { Fail("串道未取证"); return; }

        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail("EED"); return; }
        if (br.ReadBL() is < 0 or > 100_000) { Fail("num_reactors"); return; }
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();

        long fpStart = br.BitPosition;
        string filename = br.ReadTV();
        long fpEnd = br.BitPosition;
        if (filename.Length == 0 || filename.Length > MaxPathChars) { Fail("filename 越限"); return; }
        string name = br.ReadTV();
        long nameEnd = br.BitPosition;
        if (name.Length > MaxPathChars) { Fail("name 越限"); return; }

        if (br.BitPosition != hs) { Fail($"停点 {br.BitPosition - hs}"); return; }

        Cap.Filename = filename;
        Cap.Name = name;
        Cap.FilenameStartBit = fpStart;
        Cap.FilenameBitLen = (int)(fpEnd - fpStart);
        Cap.WalkEndBit = nameEnd;
        Cap.FailReason = "";
        Cap.Captured = true;
        Filename = filename;
        Name = name;
        if (!StringsCentralized)
        {
            Rec.StringFields.Add(new DwgObjectRecord.StringField(fpStart, (int)(fpEnd - fpStart), filename));
            Rec.StringFields.Add(new DwgObjectRecord.StringField(fpEnd, (int)(nameEnd - fpEnd), name));
        }
    }

    private void Fail(string why)
    {
        Cap.FailReason = why;
        Cap.WalkEndBit = -1;
    }
}
