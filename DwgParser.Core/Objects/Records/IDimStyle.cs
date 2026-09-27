namespace DwgParser.Core.Objects.Records;

public interface IDimStyle : IRecord
{
    string Name { get; }

    string Dimpost { get; }

    string Dimapost { get; }

    int Dimalt { get; }

    double Dimaltf { get; }

    int Dimaltd { get; }

    int Dimadec { get; }

    int Dimaltu { get; }

    int Dimdec { get; }

    int Dimzin { get; }

    double Dimrnd { get; }

    double Dimaltrnd { get; }

    int Dimtad { get; }

    int Dimtdec { get; }

    int DimalttD { get; }

    int Dimaunit { get; }

    int Dimfrac { get; }

    int Dimlunit { get; }

    int Dimdsep { get; }

    int Dimtmove { get; }

    bool Decoded { get; }

    bool TailDecoded { get; }

    double Dimscale { get; }

    double Dimasz { get; }

    double Dimexo { get; }

    double Dimdli { get; }

    double Dimexe { get; }

    double Dimdle { get; }

    double Dimtp { get; }

    double Dimtm { get; }

    bool Dimtol { get; }

    bool Dimlim { get; }

    bool Dimtih { get; }

    bool Dimtoh { get; }

    bool Dimse1 { get; }

    bool Dimse2 { get; }

    int Dimazin { get; }

    double Dimtxt { get; }

    double Dimcen { get; }

    double Dimtsz { get; }

    double Dimlfac { get; }

    double Dimtvp { get; }

    double Dimtfac { get; }

    double Dimgap { get; }

    bool Dimtofl { get; }

    bool Dimsah { get; }

    bool Dimtix { get; }

    bool Dimsoxd { get; }

    uint Dimclrd { get; }

    uint Dimclre { get; }

    uint Dimclrt { get; }

    string DimclrdName { get; }

    string DimclrdBookName { get; }

    string DimclreName { get; }

    string DimclreBookName { get; }

    string DimclrtName { get; }

    string DimclrtBookName { get; }

    int Dimjust { get; }

    bool Dimsd1 { get; }

    bool Dimsd2 { get; }

    int Dimtolj { get; }

    int Dimtzin { get; }

    int Dimaltz { get; }

    int Dimalttz { get; }

    bool Dimupt { get; }

    int Dimatfit { get; }

    bool Dimfxlon { get; }

    bool Flag0 { get; }

    long Dimlwd { get; }

    long Dimlwe { get; }

    double Dimfxl { get; }

    double Dimjogang { get; }

    int Dimtfill { get; }

    uint Dimtfillclr { get; }

    string DimtfillclrName { get; }

    string DimtfillclrBookName { get; }

    int Dimarcsym { get; }


    bool Dimtxtdirection { get; }

    double Dimaltmzf { get; }

    double Dimmzf { get; }

    string Dimaltmzs { get; }

    string Dimmzs { get; }
}
