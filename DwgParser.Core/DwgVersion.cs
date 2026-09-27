namespace DwgParser.Core;

public enum DwgVersion : ushort
{
    Unknown = 0,

    AC1006 = 0x1006,
    AC1009 = 0x1009,
    AC1012 = 0x1012,
    AC1014 = 0x1014,

    AC1015 = 0x1015,

    AC1018 = 0x1018,

    AC1021 = 0x1021,

    AC1024 = 0x1024,

    AC1027 = 0x1027,

    AC1032 = 0x1032,
}

public static class DwgVersionExtensions
{
    public const DwgVersion MinimumSupportedVersion = DwgVersion.AC1015;

    public static bool IsSupportedVersion(this DwgVersion version)
        => version >= MinimumSupportedVersion;

    public static string GetVersionName(this DwgVersion version)
    {
        return version switch
        {
            DwgVersion.AC1006 => "AutoCAD R12",
            DwgVersion.AC1009 => "AutoCAD R12",
            DwgVersion.AC1012 => "AutoCAD R12",
            DwgVersion.AC1014 => "AutoCAD R13",
            DwgVersion.AC1015 => "AutoCAD R14/2000",
            DwgVersion.AC1018 => "AutoCAD 2004/2005/2006",
            DwgVersion.AC1021 => "AutoCAD 2007/2008/2009",
            DwgVersion.AC1024 => "AutoCAD 2010/2011/2012",
            DwgVersion.AC1027 => "AutoCAD 2013/2014/2015/2016/2017",
            DwgVersion.AC1032 => "AutoCAD 2018+",
            _ => "Unknown"
        };
    }

    public static bool IsR2004Plus(this DwgVersion version)
        => version >= DwgVersion.AC1018;

    public static bool IsR2007Plus(this DwgVersion version)
        => version >= DwgVersion.AC1021;

    public static bool IsR2000(this DwgVersion version)
        => version == DwgVersion.AC1015;

    public static bool IsR2010Plus(this DwgVersion version)
        => version >= DwgVersion.AC1024;
}
