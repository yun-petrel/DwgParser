
namespace DwgParser.Core.Objects;

public sealed partial class DwgObjectRecord
{
    public Records.LtypeCapture? LType;

    public Records.LayerCapture? Layer;

    public List<ulong>? BlockOwnedHandles;

    public int BlockOwnedSourceCount = -1;

    public bool DataRegionRewritten;

    public Records.DictWdfltCapture? DictWdflt;

    public Records.ProxyObjectCapture? ProxyObject;
    public Records.FieldCapture? Field;

    public string? ProxyObjectTargetTail;

    public Records.DimStyleCapture? DimStyle;

    public Records.VportCapture? Vport;

    public Records.StyleCapture? Style;

    public Records.TableStyleCapture? TableStyle;

    public Records.CellStyleMapCapture? CellStyleMap;

    public Records.TableCapture? Table;

    public Records.TableContentCapture? TableContent;

    public Records.TableContentValueCapture? TableContentValue;

    public Records.PurgePreventerCapture? PurgePreventer;

    public Records.TableControlCapture? TableControl;

    public Records.MLeaderStyleCapture? MLeader;
    public List<long>? MLeaderShortCmcBits;
    public Records.VisualStyleCapture? VisualStyle;
    public Records.IVisualStyle? VisualStyleModel;

    public Records.MaterialCapture? Material;

    public Records.MLineStyleCapture? MLineStyle;

    public Records.DictVarCapture? DictVar;

    public Records.ImageDefCapture? ImageDef;

    public Records.ViewStyleCapture? ViewStyle;

    public Records.LayoutCapture? Layout;

    public Records.DimAssocCapture? DimAssoc;

    public Records.DataLinkCapture? DataLink;

    public Records.ScaleCapture? Scale;
    public Records.AppidCapture? Appid;

    public Records.XrecordCapture? Xrecord;

    public Records.SortentsTableCapture? SortentsTable;
    public Records.RasterVariablesCapture? RasterVariables;

    public Records.SunCapture? Sun;
    public Records.FieldListCapture? FieldList;

    public Records.UnderlayDefinitionCapture? UnderlayDefinition;

    public Records.RenderSettingsCapture? RenderSettings;

    public Records.TableGeometryCapture? TableGeometry;
    public Records.DataTableCapture? DataTable;


    public Records.VxTableRecordCapture? VxTableRecord;

    public Records.AssocDependencyCapture? AssocDependency;

    public Records.AssocActionCapture? AssocAction;

    public Records.AssocVertexParamCapture? AssocVertexParam;

    public Records.MLeaderObjectContextDataCapture? MLeaderObjectContextData;
}
