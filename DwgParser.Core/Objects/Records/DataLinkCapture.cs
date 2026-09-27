namespace DwgParser.Core.Objects.Records;

public sealed class DataLinkCapture
{
    public bool Captured;

    public bool ValuesCommitted;

    public string FailReason = "";
    public long FailBit = -1;

    public long HsBit;
    public long StringAreaStartBit;
    public long StringAreaEndBit;
    public long EedStartBit;
    public int EedBitLen;

    public string DataAdapter = "";
    public string Description = "";
    public string Tooltip = "";
    public string ConnectionString = "";
    public string UpdateStatus = "";

    public long Option;
    public long UpdateOption;
    public long Bl92;
    public long[] Timestamp = new long[7];
    public long PathOption;
    public long Bl93;

    public long NumCustomdata;

    public int StringCount;
}
