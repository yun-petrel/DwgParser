using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class ImageDefCentralRecord : ImageDefRecord
{
    protected ImageDefCentralRecord(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    internal override void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        if (!TryBeginFrame(br, out long ss, out long dataEnd, out BitReader? strBr))
        {
            Fail(br, FrameFailName);
            return;
        }

        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();
        long classVersion = br.ReadBL();
        if (classVersion is < 0 or > 10) { Fail(br, "class_version>10"); return; }
        var imageSize = br.Read2RD();
        bool isLoaded = br.ReadB();
        int resUnits = br.ReadRC();
        var pixelSize = br.Read2RD();

        long expected = ss > 0 ? ss : dataEnd;
        if (br.BitPosition != expected)
        { Fail(br, br.BitPosition < expected ? "数值区短于串区起点" : "数值区越过串区起点"); return; }

        var strs = StringStreamOf(strBr);
        if (strs is null) { Fail(br, "串流未挂接"); return; }
        long strBegin = strs.BitPosition;
        string filePath = strs.ReadTV();
        long strEnd = strs.BitPosition;
        long consumed = strEnd - strBegin;
        if (consumed != dataEnd - 17 - ss && consumed != dataEnd - 33 - ss)
        { Fail(br, consumed < 0 ? "串流位置异常" : "串区未被一枚 TU 吃完"); return; }

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg)
            System.Console.Error.WriteLine(
                $"IMGDEF-C h={Rec.Handle:X} fam={Family} ss={ss} dataEnd={dataEnd} cv={classVersion}" +
                $" size=({imageSize.X},{imageSize.Y}) px=({pixelSize.X},{pixelSize.Y})" +
                $" loaded={isLoaded} units={resUnits} path='{filePath}' str[{strBegin}..{strEnd}]");

        Cap.FilePath = filePath;
        CommitValues(classVersion, imageSize, isLoaded, resUnits, pixelSize);
        FilePath = filePath;
        ClassVersion = (int)classVersion;
        Cap.FailReason = "";
        Cap.FailBit = -1;
    }

    protected virtual bool TryBeginFrame(BitReader br, out long ss, out long dataEnd,
        out BitReader? strBr)
        => CentralFrame.TryRlFrame(Rec, ObjStart, br, out ss, out dataEnd, out strBr);

    protected virtual string FrameFailName => "帧式或串区不合(AC1021 RL 帧)";

    protected virtual bool HasDsData => false;

    private static BitReader? StringStreamOf(BitReader? strBr) =>
        strBr is null ? null : strBr.StringStream ?? strBr;

    protected void Fail(BitReader br, string why)
    {
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"IMGDEF-F h={Rec.Handle:X} fam={Family} why={why} rel={br.BitPosition - Rec.CarryStartBit}" +
                $" hs={Rec.HandleStreamStartBit} ss={Rec.StringStreamStartBit}");
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }
}

internal abstract class ImageDefAcR2010Plus : ImageDefCentralRecord
{
    protected ImageDefAcR2010Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool TryBeginFrame(BitReader br, out long ss, out long dataEnd,
        out BitReader? strBr)
    {
        ss = 0;
        dataEnd = 0;
        strBr = null;
        if (Rec.HandleStreamStartBit <= 0 || !Rec.HasHandleStream) { frameFail = "帧界(公共路径未给 HS 锚)"; return false; }
        if (Rec.StringStreamStartBit <= 0) { frameFail = "无串区(has_strings=0)"; return false; }
        frameFail = "锚在、CentralFrame 仍不合";
        return CentralFrame.TryBotFrame(Rec, br, out ss, out dataEnd, out strBr);
    }

    protected override string FrameFailName => frameFail;

    protected override bool HasDsData => true;

    private string frameFail = "";
}

internal sealed class ImageDefAcR2007 : ImageDefCentralRecord
{
    public ImageDefAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ImageDefAcR2007";
}

internal sealed class ImageDefAcR2010 : ImageDefAcR2010Plus
{
    public ImageDefAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ImageDefAcR2010";

    protected override bool HasDsData => false;
}

internal sealed class ImageDefAcR2013 : ImageDefAcR2010Plus
{
    public ImageDefAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ImageDefAcR2013";
}

internal sealed class ImageDefAcR2018 : ImageDefAcR2010Plus
{
    public ImageDefAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ImageDefAcR2018";
}
