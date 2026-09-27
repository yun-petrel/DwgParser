using DwgParser.Core;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public abstract class ToleranceEntity : DwgEntity, ITolerance
{
    public double DirX { get; protected set; } = 1.0;
    public double DirY { get; protected set; }
    public double DirZ { get; protected set; }
    public string ToleranceText { get; protected set; } = string.Empty;
    public int ContentSize => ToleranceText.Length;

    public void ReadBody(BitReader br, DwgObjectRecord? record, DwgVersion version,
                         ulong handle, int objStart)
    {
        Kind = DwgEntityType.Tolerance;
        var ins = br.Read3BD();
        var dir = br.Read3BD();
        var nor = br.Read3BD();
        if (!double.IsFinite(ins.X) || !double.IsFinite(ins.Y) || !double.IsFinite(dir.X)
            || !double.IsFinite(nor.Z))
            throw new EndOfStreamException();
        long textStart = br.BitPosition;
        string text = br.ReadTV();

        DirX = dir.X; DirY = dir.Y; DirZ = dir.Z;
        Dir = new Vec2(dir.X, dir.Y);
        ToleranceText = text;
        Start = new Vec2(ins.X, ins.Y);
        Elevation = ins.Z;
        ExtrusionZ = nor.Z;
        TextValue = text;

        if (record == null) return;
        CaptureContentString(br, record, handle, textStart, text);
    }

    protected virtual void CaptureContentString(BitReader br, DwgObjectRecord record,
        ulong handle, long textStart, string text)
    {
    }
}

internal sealed class ToleranceAcR2000Plus : ToleranceEntity
{
    protected override void CaptureContentString(BitReader br, DwgObjectRecord record,
        ulong handle, long textStart, string text)
    {
        record.CaptureString(textStart, br.BitPosition, text);
        if (DwgObjectStreamWriter.DebugTrace && text.Length > 0)
            System.Console.Error.WriteLine(
                $"DWGDBG TOL4INLINE h={handle:X} text=\"{text}\" bits[{textStart},{br.BitPosition})");
    }
}

internal sealed class ToleranceAcR2007Plus : ToleranceEntity
{
    protected override void CaptureContentString(BitReader br, DwgObjectRecord record,
        ulong handle, long textStart, string text)
    {
        record.StringFields.Add(new DwgObjectRecord.StringField(textStart, 0, text));
        if (DwgObjectStreamWriter.DebugTrace && text.Length > 0)
            System.Console.Error.WriteLine(
                $"DWGDBG TOLC h={handle:X} text=\"{text}\" anchor={textStart}");
    }
}
