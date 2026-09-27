namespace DwgParser.Core.Objects.Records;

public sealed class ImageDefCapture : RecordIdentity, IImageDef
{
    public string FilePath = "";

    public long FilePathStartBit;
    public int FilePathBitLen;

    public int ClassVersion;

    public double ImageSizeX, ImageSizeY;
    public double PixelSizeX, PixelSizeY;
    public bool IsLoaded;
    public int ResUnits;

    public long HsBit;

    public bool Captured;

    public bool ValuesCommitted;

    public string FailReason = "";
    public long FailBit = -1;

    string IImageDef.FilePath => FilePath;
    int IImageDef.ClassVersion => ClassVersion;
    bool IImageDef.Captured => Captured;
}
