using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class ImageDefRecord : IImageDef
{
    public const short TypeCodeValue = 543;

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected ImageDefRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.ImageDef ??= new ImageDefCapture();
        Cap.AttachIdentity(rec);
    }

    public ImageDefCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string FilePath { get; protected set; } = "";
    public int ClassVersion { get; protected set; }
    public bool Captured => Cap.Captured;

    internal virtual void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        long ownStart = br.BitPosition;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return;
        if (br.ReadBL() is < 0 or > 8) return;
        if (HasXdicMissing) br.ReadB();

        long classVersionStart = br.BitPosition;
        long classVersion = br.ReadBL();
        if (classVersion is < 0 or > 10) return;
        var imageSize = br.Read2RD();

        long fpStart = br.BitPosition;
        string filePath = br.ReadTV();
        long fpEnd = br.BitPosition;
        bool isLoaded = br.ReadB();
        int resUnits = br.ReadRC();
        var pixelSize = br.Read2RD();
        long end = br.BitPosition;

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg)
            System.Console.Error.WriteLine(
                $"IMGDEF h={Rec.Handle:X} t={Rec.TypeCode} hs={hs} cv={classVersion}" +
                $" cvStart={classVersionStart - ownStart} file_path='{filePath}'" +
                $"[{fpStart - ownStart}..{fpEnd - ownStart}] endRel={end - ownStart}" +
                $" size=({imageSize.X},{imageSize.Y}) px=({pixelSize.X},{pixelSize.Y})" +
                $" loaded={isLoaded} units={resUnits}");

        if (end != hs || fpStart < ownStart || fpEnd > hs) return;

        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        Rec.StringFields.Add(new DwgObjectRecord.StringField(fpStart, (int)(fpEnd - fpStart), filePath));
        Cap.FilePath = filePath;
        Cap.FilePathStartBit = fpStart;
        Cap.FilePathBitLen = (int)(fpEnd - fpStart);
        Cap.ClassVersion = (int)classVersion;
        Cap.HsBit = hs;
        Cap.Captured = true;
        CommitValues(classVersion, imageSize, isLoaded, resUnits, pixelSize);
        FilePath = filePath;
        ClassVersion = (int)classVersion;
    }

    protected void CommitValues(long classVersion, (double X, double Y) imageSize, bool isLoaded,
        int resUnits, (double X, double Y) pixelSize)
    {
        Cap.ClassVersion = (int)classVersion;
        (Cap.ImageSizeX, Cap.ImageSizeY) = imageSize;
        (Cap.PixelSizeX, Cap.PixelSizeY) = pixelSize;
        Cap.IsLoaded = isLoaded;
        Cap.ResUnits = resUnits;
        Cap.ValuesCommitted = true;
    }

    protected virtual bool HasXdicMissing => true;
}
