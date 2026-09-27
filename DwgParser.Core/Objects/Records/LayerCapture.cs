using System.Collections.Generic;

namespace DwgParser.Core.Objects.Records;

public sealed class LayerCapture : RecordIdentity, ILayer
{
    public string Name { get; private set; } = "";

    public int ColorIndex { get; private set; }

    public uint TrueColor { get; private set; }

    public string ColorName { get; private set; } = "";

    public string ColorBookName { get; private set; } = "";

    public short? LayerColor { get; private set; }


    public bool Frozen { get; private set; }

    public bool Off { get; private set; }

    public bool FrozenInNew { get; private set; }

    public bool Locked { get; private set; }

    public bool? PlotFlag { get; private set; }

    public int? LineWeightRaw { get; private set; }

    public int Dxf70 { get; private set; }


    public bool XrefRef { get; private set; }

    public int XrefResolved { get; private set; }

    public bool XrefDep { get; private set; }

    public bool HasName => Name.Length > 0;

    public ulong DefaultMaterialHandle { get; set; }


    internal void BeginFrame(DwgObjectRecord rec)
    {
        AttachIdentity(rec);
        Name = "";
        ColorIndex = 0;
        TrueColor = 0;
        ColorName = "";
        ColorBookName = "";
        LayerColor = null;
        Frozen = false;
        Off = false;
        FrozenInNew = false;
        Locked = false;
        PlotFlag = null;
        LineWeightRaw = null;
        Dxf70 = 0;
        XrefRef = false;
        XrefResolved = 0;
        XrefDep = false;
        _objectHandles.Clear();
    }

    private readonly List<ulong> _objectHandles = new();

    public IReadOnlyList<ulong> ObjectHandles => _objectHandles;

    internal void CommitObjectHandle(ulong resolved) => _objectHandles.Add(resolved);

    internal void CommitName(string name) => Name = name;

    internal void CommitXref(bool xrefRef, int resolved, bool xrefDep)
    {
        XrefRef = xrefRef;
        XrefResolved = resolved;
        XrefDep = xrefDep;
    }

    internal void CommitFlags(bool frozen, bool off, bool frozenInNew, bool locked)
    {
        Frozen = frozen;
        Off = off;
        FrozenInNew = frozenInNew;
        Locked = locked;
    }

    internal void CommitPlotAndLineWeight(bool plotFlag, int lineWeightRaw)
    {
        PlotFlag = plotFlag;
        LineWeightRaw = lineWeightRaw;
    }

    public long ColorFieldStartBit { get; private set; }
    public long ColorFieldEndBit { get; private set; }

    internal void CommitColorSpan(long start, long end) { ColorFieldStartBit = start; ColorFieldEndBit = end; }

    internal void CommitColor(int colorIndex, uint trueColor, string colorName, string colorBookName)
    {
        ColorIndex = colorIndex;
        TrueColor = trueColor;
        ColorName = colorName;
        ColorBookName = colorBookName;
    }

    internal void CommitLayerColor(short aci) => LayerColor = aci;

    internal void CommitDxf70() => Dxf70 =
        (Frozen ? 1 : 0) | (FrozenInNew ? 2 : 0) | (Locked ? 4 : 0)
        | (XrefRef ? 16 : 0) | (XrefResolved != 0 ? 32 : 0) | (XrefDep ? 64 : 0);

    string ILayer.Name => Name;
    int ILayer.ColorIndex => ColorIndex;
    uint ILayer.TrueColor => TrueColor;
    string ILayer.ColorName => ColorName;
    string ILayer.ColorBookName => ColorBookName;
    short? ILayer.LayerColor => LayerColor;
    bool ILayer.Captured => HasName;
    bool ILayer.Frozen => Frozen;
    bool ILayer.Off => Off;
    bool ILayer.FrozenInNew => FrozenInNew;
    bool ILayer.Locked => Locked;
    bool? ILayer.PlotFlag => PlotFlag;
    int? ILayer.LineWeightRaw => LineWeightRaw;
    int ILayer.Dxf70 => Dxf70;
    bool ILayer.XrefRef => XrefRef;
    int ILayer.XrefResolved => XrefResolved;
    bool ILayer.XrefDep => XrefDep;
}
