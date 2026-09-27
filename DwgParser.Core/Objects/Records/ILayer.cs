namespace DwgParser.Core.Objects.Records;

public interface ILayer : IRecord
{
    string Name { get; }

    int ColorIndex { get; }

    uint TrueColor { get; }

    string ColorName { get; }

    string ColorBookName { get; }

    short? LayerColor { get; }

    bool Captured { get; }


    bool Frozen { get; }

    bool Off { get; }

    bool FrozenInNew { get; }

    bool Locked { get; }

    bool? PlotFlag { get; }

    int? LineWeightRaw { get; }

    int Dxf70 { get; }


    bool XrefRef { get; }

    int XrefResolved { get; }

    bool XrefDep { get; }
}
