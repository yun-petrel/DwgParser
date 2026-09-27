using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    public sealed class TextStyleInfo : IStyle
    {
        public ulong Handle { get; set; }
        public short TypeCode { get; set; } = 0x35;
        public DwgVersion SourceVersion { get; set; }
        public int XrefResolved { get; set; }
        public bool Captured { get; set; }
        public int Generation { get; set; }
        public double LastHeight { get; set; }
        public string Name { get; set; } = "";
        public string FontFile { get; set; } = "";
        public string BigfontFile { get; set; } = "";
        public bool IsShape { get; set; }
        public bool IsVertical { get; set; }
        public double FixedHeight { get; set; }
        public double WidthFactor { get; set; } = 1.0;
        public double ObliqueAngle { get; set; }
    }

    public sealed class LinetypeInfo : ILtype
    {
        public ulong Handle { get; set; }
        public short TypeCode { get; set; } = LtypeRecord.TypeCodeValue;
        public DwgVersion SourceVersion { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int XrefResolved { get; set; }
        public int NumDashes { get; set; }
        public bool Captured { get; set; } = true;
        public double PatternLen { get; set; }
        public int Alignment { get; set; } = 65;
        public System.Collections.Generic.List<LtypeRecord.LtypeDash> Dashes { get; } = new();
    }

    public sealed class DimStyleInfo : IDimStyle
    {
        public ulong Handle { get; set; }
        public short TypeCode { get; set; } = DimStyleRecord.TypeCodeValue;
        public DwgVersion SourceVersion { get; set; }
        public string Name { get; set; } = "";
        public string Dimpost { get; set; } = "";
        public string Dimapost { get; set; } = "";
        public int Dimalt { get; set; }
        public double Dimaltf { get; set; } = 1.0;
        public int Dimaltd { get; set; } = 2;
        public int Dimadec { get; set; } = 2;
        public int Dimaltu { get; set; } = 2;
        public int Dimdec { get; set; } = 2;
        public int Dimzin { get; set; }
        public double Dimrnd { get; set; }
        public double Dimaltrnd { get; set; }
        public int Dimtad { get; set; }
        public int Dimtdec { get; set; } = 2;
        public int DimalttD { get; set; } = 2;
        public int Dimaunit { get; set; }
        public int Dimfrac { get; set; } = 1;
        public int Dimlunit { get; set; } = 2;
        public int Dimdsep { get; set; } = 46;
        public int Dimtmove { get; set; }
        public bool TailDecoded { get; set; }
        public bool Decoded { get; set; }

        public double Dimscale { get; set; } = 1.0;
        public double Dimasz { get; set; } = 2.5;
        public double Dimexo { get; set; } = 6.25;
        public double Dimdli { get; set; } = 3.75;
        public double Dimexe { get; set; } = 1.25;
        public double Dimdle { get; set; }
        public double Dimtp { get; set; }
        public double Dimtm { get; set; }
        public bool Dimtol { get; set; }
        public bool Dimlim { get; set; }
        public bool Dimtih { get; set; } = true;
        public bool Dimtoh { get; set; } = true;
        public bool Dimse1 { get; set; }
        public bool Dimse2 { get; set; }
        public int Dimazin { get; set; }
        public double Dimtxt { get; set; } = 2.5;
        public double Dimcen { get; set; } = 2.5;
        public double Dimtsz { get; set; }
        public double Dimlfac { get; set; } = 1.0;
        public double Dimtvp { get; set; }
        public double Dimtfac { get; set; } = 1.0;
        public double Dimgap { get; set; } = 6.25;
        public bool Dimtofl { get; set; }
        public bool Dimsah { get; set; }
        public bool Dimtix { get; set; }
        public bool Dimsoxd { get; set; }
        public uint Dimclrd { get; set; }
        public uint Dimclre { get; set; }
        public uint Dimclrt { get; set; }

        public string DimclrdName { get; set; } = "";
        public string DimclrdBookName { get; set; } = "";
        public string DimclreName { get; set; } = "";
        public string DimclreBookName { get; set; } = "";
        public string DimclrtName { get; set; } = "";
        public string DimclrtBookName { get; set; } = "";
        public string DimtfillclrName { get; set; } = "";
        public string DimtfillclrBookName { get; set; } = "";
        public int Dimjust { get; set; }
        public bool Dimsd1 { get; set; }
        public bool Dimsd2 { get; set; }
        public int Dimtolj { get; set; }
        public int Dimtzin { get; set; }
        public int Dimaltz { get; set; }
        public int Dimalttz { get; set; }
        public bool Dimupt { get; set; }
        public int Dimatfit { get; set; } = 3;
        public bool Dimfxlon { get; set; }

        public bool Flag0 { get; set; }
        public long Dimlwd { get; set; } = -2;
        public long Dimlwe { get; set; } = -2;
        public double Dimfxl { get; set; }
        public double Dimjogang { get; set; }
        public int Dimtfill { get; set; }
        public uint Dimtfillclr { get; set; }
        public int Dimarcsym { get; set; }

        public bool Dimtxtdirection { get; set; }
        public double Dimaltmzf { get; set; }
        public double Dimmzf { get; set; }
        public string Dimaltmzs { get; set; } = "";
        public string Dimmzs { get; set; } = "";
    }
}
