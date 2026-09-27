namespace DwgParser.Core.Objects.Records;

public sealed class XrecordCapture : RecordIdentity
{
    public List<XrecordItem> Items { get; } = new();

    public int XdataSize;

    public int Cloning;

    public bool XdicMissing;

    public bool Captured;

    public string FailReason = "";
    public bool XdataBeyondDeclared;

    public bool DataBeyondFrame;

    public bool CloningBeyondFrame;

    public long FailBit = -1;

    public long DataStartBit;
    public long DataEndBit;


    public XrecordItem? FindByCode(int code)
        => Items.FirstOrDefault(i => i.Code == code);

    public IReadOnlyList<XrecordItem> FindAllByCode(int code)
        => Items.Where(i => i.Code == code).ToList();

    public bool TryGetInteger(int code, out long value)
    {
        var item = FindByCode(code);
        if (item?.TryAsInteger(out value) == true) return true;
        value = 0;
        return false;
    }

    public bool TryGetDouble(int code, out double value)
    {
        var item = FindByCode(code);
        if (item?.TryAsDouble(out value) == true) return true;
        value = 0;
        return false;
    }

    public bool TryGetString(int code, out string value)
    {
        var item = FindByCode(code);
        if (item?.Text != null) { value = item.Text; return true; }
        value = "";
        return false;
    }

    public bool TryGetPoint(int code, out double x, out double y, out double z)
    {
        var item = FindByCode(code);
        if (item?.TryAsPoint(out x, out y, out z) == true) return true;
        x = y = z = 0;
        return false;
    }

    public bool TryGetHandle(int code, out ulong handle)
    {
        var item = FindByCode(code);
        if (item?.TryAsHandle(out handle) == true) return true;
        handle = 0;
        return false;
    }

    public bool TryGetBinary(int code, out byte[] data)
    {
        var item = FindByCode(code);
        if (item?.TryAsBinary(out data) == true) return true;
        data = Array.Empty<byte>();
        return false;
    }

    public IReadOnlyCollection<int> DistinctCodes
        => Items.Select(i => i.Code).Distinct().OrderBy(c => c).ToList();

    public IReadOnlyDictionary<int, int> CodeHistogram
        => Items.GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
}

public sealed class XrecordItem
{
    public int Code;

    public int BitLen;

    public string? Text;

    public object? Number;

    public byte[]? Binary;

    public double[]? Point;

    public ulong HandleValue;

    public bool IsHandle;


    public bool TryAsInteger(out long value)
    {
        if (Number is long l) { value = l; return true; }
        if (Number is double d) { value = (long)d; return true; }
        value = 0;
        return false;
    }

    public bool TryAsDouble(out double value)
    {
        if (Number is double d) { value = d; return true; }
        if (Number is long l) { value = l; return true; }
        value = 0;
        return false;
    }

    public bool TryAsPoint(out double x, out double y, out double z)
    {
        if (Point is { Length: 3 } p)
        {
            x = p[0]; y = p[1]; z = p[2];
            return true;
        }
        x = y = z = 0;
        return false;
    }

    public bool TryAsHandle(out ulong handle)
    {
        if (IsHandle) { handle = HandleValue; return true; }
        handle = 0;
        return false;
    }

    public bool TryAsBinary(out byte[] data)
    {
        if (Binary is { } b) { data = b; return true; }
        data = Array.Empty<byte>();
        return false;
    }

    public string ValueType => IsHandle ? "handle"
        : Point != null ? "point3d"
        : Binary != null ? "binary"
        : Text != null ? "string"
        : Number is double ? "real"
        : Number != null ? "integer"
        : "unknown";
}
