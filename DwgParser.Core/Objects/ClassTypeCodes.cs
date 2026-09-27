namespace DwgParser.Core.Objects;

public static class ClassTypeCodes
{
    public delegate string? TypeNameResolver(int typeCode);

    private static readonly Dictionary<string, short> CanonicalByClass = new()
    {
        ["DICTIONARYWDFLT"] = 500,
        ["DICTIONARYWITHDEFAULT"] = 500,
        ["TABLESTYLE"] = 504,
        ["DICTIONARYVAR"] = 503,
        ["SCALE"] = 506,
        ["MLEADERSTYLE"] = 507,
        ["MATERIAL"] = 508,
        ["VISUALSTYLE"] = 509,
        ["SECTIONVIEWSTYLE"] = 510,
        ["DETAILVIEWSTYLE"] = 511,
        ["CELLSTYLEMAP"] = 512,
        ["LAYOUT"] = 0x52,
        ["TABLE"] = 530,
        ["TABLECONTENT"] = 531,
        ["TABLEGEOMETRY"] = 532,
        ["DATATABLE"] = 533,
        ["MULTILEADER"] = 534,
        ["IMAGEDEF"] = 543,
        ["DIMASSOC"] = 518,
        ["SORTENTSTABLE"] = 517,
        ["RASTERVARIABLES"] = 542,
        ["SUN"] = 552,
        ["FIELDLIST"] = 527,
        ["FIELD"] = 568,
        ["PDFDEFINITION"] = 544,
        ["_MLEADEROBJECTCONTEXTDATA_CLASS"] = 567,
        ["DATALINK"] = 565,
        ["_DATALINK"] = 565,
        ["LWPOLYLINE"] = 0x4D,
        ["HATCH"] = 0x4E,
        ["WIPEOUT"] = 0x4C,
        ["ASSOCVERTEXACTIONPARAM"] = 523,
        ["ASSOCNETWORK"] = 520,
        ["ASSOCACTION"] = 521,
        ["ASSOCDEPENDENCY"] = 524,
        ["ASSOCGEOMDEPENDENCY"] = 529,
    };

    public static string Normalize(string? dxfName)
    {
        if (string.IsNullOrWhiteSpace(dxfName)) return "";
        var n = dxfName.Trim().ToUpperInvariant();
        if (n.StartsWith("ACDB")) n = n[4..];
        else if (n.StartsWith("ACAD_")) n = n[5..];
        return n;
    }

    public static string? CanonicalClassName(string? dxfName)
    {
        var n = Normalize(dxfName);
        return n.Length > 0 && CanonicalByClass.ContainsKey(n) ? n : null;
    }

    public static string DiagnosticClassOf(short typeCode, string? dxfName)
    {
        var n = Normalize(dxfName);
        if (n.Length > 0) return n;
        return Format.R2000TypeNames.FixedTypeNames.TryGetValue(typeCode, out var fx)
            ? fx : "fix" + typeCode;
    }

    public static short Canonical(short typeCode, string? dxfName)
    {
        if (typeCode < 500) return typeCode;
        var n = Normalize(dxfName);
        if (n.Length == 0) return typeCode;
        return CanonicalByClass.TryGetValue(n, out var c) ? c : (short)(typeCode + 1000);
    }
}
