using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class MLineStyleRecord : IMLineStyle
{
    public const short TypeCodeValue = 0x49;

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected MLineStyleRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.MLineStyle ??= new MLineStyleCapture();
        Cap.AttachIdentity(rec);
    }

    public MLineStyleCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name { get; protected set; } = "";
    public string Description { get; protected set; } = "";
    public bool Captured => Cap.Captured;

    public bool SemanticCaptured { get; private set; }
    public int Flag { get; private set; }
    public (int Idx, uint Rgb, byte Flag) FillColor { get; private set; }
    public double StartAngle { get; private set; }
    public double EndAngle { get; private set; }
    public MLineStyleCapture.MLineStyleLine[]? Lines { get; private set; }

    internal virtual void Read(BitReader br)
    {
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        long ownStart = br.BitPosition;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return;
        long eedLen = br.BitPosition - eedStart;
        br.ReadBL();
        if (HasXdicMissing) br.ReadB();

        long nameStart = br.BitPosition;
        string name = br.ReadTV();
        long nameEnd = br.BitPosition;

        long descStart = nameEnd;
        string desc = br.ReadTV();
        long descEnd = br.BitPosition;

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg) System.Console.Error.WriteLine(
            $"MLINEREC h={Rec.Handle:X} t={Rec.TypeCode} hs={hs} name='{name}'[{nameStart - ownStart}..{nameEnd - ownStart}] desc='{desc}'[{descStart - ownStart}..{descEnd - ownStart}]");

        if (name.Length == 0 || nameStart < ownStart || descEnd > hs || descStart < nameEnd) return;

        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        Rec.EedStartBit = eedStart;
        Rec.EedBitLen = (int)eedLen;
        Rec.StringFields.Add(new DwgObjectRecord.StringField(nameStart, (int)(nameEnd - nameStart), name));
        Rec.StringFields.Add(new DwgObjectRecord.StringField(descStart, (int)(descEnd - descStart), desc));
        Cap.Name = name;
        Cap.Description = desc;
        Cap.NameStartBit = nameStart;
        Cap.NameBitLen = (int)(nameEnd - nameStart);
        Cap.DescStartBit = descStart;
        Cap.DescBitLen = (int)(descEnd - descStart);
        Cap.EedStartBit = eedStart;
        Cap.EedBitLen = (int)eedLen;
        Cap.Captured = true;
        Name = name;
        Description = desc;

        try
        {
            int flag = br.ReadBS();
            long cFill = br.BitPosition;
            var fill = ReadCmcVersioned(br);
            Cap.CmcSpans.Add((cFill, br.BitPosition));
            double startAngle = br.ReadBD();
            double endAngle = br.ReadBD();
            int numLines = br.ReadRC();
            bool ok = numLines is >= 0 and <= 256
                && double.IsFinite(startAngle) && double.IsFinite(endAngle);
            List<MLineStyleCapture.MLineStyleLine>? lines
                = ok ? new List<MLineStyleCapture.MLineStyleLine>(numLines) : null;
            for (int i = 0; ok && i < numLines; i++)
            {
                if (br.BitPosition + 8 > hs) { ok = false; break; }
                double offset = br.ReadBD();
                long cLine = br.BitPosition;
                var col = ReadCmcVersioned(br);
                Cap.CmcSpans.Add((cLine, br.BitPosition));
                int ltIndex = br.ReadBS();
                if (!double.IsFinite(offset)) { ok = false; break; }
                lines!.Add(new MLineStyleCapture.MLineStyleLine
                {
                    Offset = offset,
                    ColorIdx = col.Item1, ColorRgb = col.Item2, ColorFlag = col.Item3,
                    LtIndex = ltIndex,
                });
            }
            if (ok && lines is not null && br.BitPosition == hs)
            {
                Cap.SemanticCaptured = true;
                Cap.Flag = flag;
                Cap.FillIdx = fill.Item1; Cap.FillRgb = fill.Item2; Cap.FillFlag = fill.Item3;
                Cap.StartAngle = startAngle;
                Cap.EndAngle = endAngle;
                Cap.Lines = lines.ToArray();
                SemanticCaptured = true;
                Flag = flag;
                FillColor = fill;
                StartAngle = startAngle;
                EndAngle = endAngle;
                Lines = lines.ToArray();
            }
        }
        catch {  }
    }

    private (int Idx, uint Rgb, byte Flag) ReadCmcVersioned(BitReader br)
        => Rec.SourceVersion >= DwgVersion.AC1018
            ? DwgObjectReader.ReadCmcTriple(br)
            : (br.ReadBS(), 0u, (byte)0);

    protected virtual bool HasXdicMissing => true;
}
