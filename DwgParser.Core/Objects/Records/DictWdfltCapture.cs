namespace DwgParser.Core.Objects.Records;

public sealed class DictWdfltCapture : RecordIdentity, IDictWdflt
{
    public long NumEntries;

    public string EntryName = "";

    public List<string> EntryNames = new();

    public long EntryNameStartBit;
    public int EntryNameBitLen;

    public long DuplicateCloning;

    public int HardOwner;

    public bool Captured;

    public bool ValuesCommitted;

    public string DefaultEntryName = "";

    public string FailReason = "";
    public long FailBit = -1;

    long IDictWdflt.NumEntries => NumEntries;
    string IDictWdflt.EntryName => EntryName;
    bool IDictWdflt.Captured => Captured;
    long IDictWdflt.DuplicateCloning => DuplicateCloning;
    int IDictWdflt.HardOwner => HardOwner;
}
