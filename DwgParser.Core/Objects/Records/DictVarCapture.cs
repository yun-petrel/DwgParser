namespace DwgParser.Core.Objects.Records;

public sealed class DictVarCapture : RecordIdentity, IDictVar
{
    public string StrValue = "";

    public long StrStartBit;
    public int StrBitLen;

    public int Schema;

    public long EedStartBit;
    public int EedBitLen;

    public bool Captured;

    string IDictVar.StrValue => StrValue;
    bool IDictVar.Captured => Captured;
    int IDictVar.Schema => Schema;
}
