namespace DwgParser.Core.Format;

public static class R2000TypeNames
{
    public static readonly Dictionary<int, string> FixedTypeNames = new()
    {
        [0x01] = "TEXT", [0x02] = "ATTRIB", [0x03] = "ATTDEF", [0x04] = "BLOCK",
        [0x05] = "ENDBLK", [0x06] = "SEQEND", [0x07] = "INSERT", [0x08] = "MINSERT",
        [0x0A] = "VERTEX_2D", [0x0B] = "VERTEX_3D", [0x0C] = "VERTEX_MESH",
        [0x0D] = "VERTEX_PFACE", [0x0E] = "VERTEX_PFACE_FACE",
        [0x0F] = "POLYLINE_2D", [0x10] = "POLYLINE_3D",
        [0x11] = "ARC", [0x12] = "CIRCLE", [0x13] = "LINE",
        [0x14] = "DIM_ORDINATE", [0x15] = "DIM_LINEAR", [0x16] = "DIM_ALIGNED",
        [0x17] = "DIM_ANG3PT", [0x18] = "DIM_ANG2LN", [0x19] = "DIM_RADIUS",
        [0x1A] = "DIM_DIAMETER", [0x1B] = "POINT", [0x1C] = "3DFACE",
        [0x1D] = "POLYLINE_PFACE", [0x1E] = "POLYLINE_MESH", [0x1F] = "SOLID",
        [0x20] = "TRACE", [0x21] = "SHAPE", [0x22] = "VIEWPORT",
        [0x23] = "ELLIPSE", [0x24] = "SPLINE", [0x25] = "REGION",
        [0x26] = "3DSOLID", [0x27] = "BODY", [0x28] = "RAY", [0x29] = "XLINE",
        [0x2A] = "DICTIONARY", [0x2B] = "OLEFRAME", [0x2C] = "MTEXT",
        [0x2D] = "LEADER", [0x2E] = "TOLERANCE", [0x2F] = "MLINE",
        [0x30] = "BLOCK_CONTROL", [0x31] = "BLOCK_HEADER", [0x32] = "LAYER_CONTROL",
        [0x33] = "LAYER", [0x34] = "STYLE_CONTROL", [0x35] = "STYLE",
        [0x38] = "LTYPE_CONTROL", [0x39] = "LTYPE",
        [0x3C] = "VIEW_CONTROL", [0x3D] = "VIEW", [0x3E] = "UCS_CONTROL",
        [0x3F] = "UCS", [0x40] = "VPORT_CONTROL", [0x41] = "VPORT",
        [0x42] = "APPID_CONTROL", [0x43] = "APPID",
        [0x44] = "DIMSTYLE_CONTROL", [0x45] = "DIMSTYLE",
        [0x46] = "VX_CONTROL", [0x47] = "VX_TABLE_RECORD",
        [0x48] = "GROUP", [0x49] = "MLINESTYLE", [0x4A] = "OLE2FRAME",
        [0x4B] = "DUMMY", [0x4C] = "LONG_TRANSACTION", [0x4D] = "LWPOLYLINE",
        [0x4E] = "HATCH", [0x4F] = "XRECORD", [0x50] = "PLACEHOLDER",
        [0x51] = "VBA_PROJECT", [0x52] = "LAYOUT",
    };

    private static readonly Dictionary<short, string> FixedDisplayNames = new()
    {
        [0x01] = "Text", [0x02] = "Attrib", [0x03] = "AttDef", [0x04] = "Block",
        [0x05] = "EndBlk", [0x06] = "SeqEnd", [0x07] = "Insert", [0x08] = "MInsert",
        [0x0A] = "Vertex2D", [0x0B] = "Vertex3D", [0x0C] = "VertexMesh",
        [0x0D] = "VertexPFace", [0x0E] = "VertexPFaceFace",
        [0x0F] = "Polyline2D", [0x10] = "Polyline3D",
        [0x11] = "Arc", [0x12] = "Circle", [0x13] = "Line",
        [0x14] = "DimOrdinate", [0x15] = "DimLinear", [0x16] = "DimAligned",
        [0x17] = "DimAng3pt", [0x18] = "DimAng2ln", [0x19] = "DimRadius",
        [0x1A] = "DimDiameter", [0x1B] = "Point", [0x1C] = "3dFace",
        [0x1D] = "PolylinePFace", [0x1E] = "PolylineMesh", [0x1F] = "Solid",
        [0x20] = "Trace", [0x21] = "Shape", [0x22] = "Viewport",
        [0x23] = "Ellipse", [0x24] = "Spline", [0x25] = "Region",
        [0x26] = "Solid3d", [0x27] = "Body", [0x28] = "Ray", [0x29] = "XLine",
        [0x2A] = "Dictionary", [0x2B] = "OleFrame", [0x2C] = "MText",
        [0x2D] = "Leader", [0x2E] = "Tolerance", [0x2F] = "Mline",
        [0x30] = "BlockControl", [0x31] = "BlockHeader", [0x32] = "LayerControl",
        [0x33] = "Layer", [0x34] = "StyleControl", [0x35] = "Style",
        [0x38] = "LtypeControl", [0x39] = "Ltype",
        [0x3C] = "ViewControl", [0x3D] = "View", [0x3E] = "UcsControl",
        [0x3F] = "Ucs", [0x40] = "VportControl", [0x41] = "Vport",
        [0x42] = "AppidControl", [0x43] = "Appid",
        [0x44] = "DimstyleControl", [0x45] = "Dimstyle",
        [0x46] = "VxControl", [0x47] = "VxTableRecord",
        [0x48] = "Group", [0x49] = "MlineStyle", [0x4A] = "Ole2Frame",
        [0x4B] = "Dummy", [0x4C] = "LongTransaction", [0x4D] = "LwPolyline",
        [0x4E] = "Hatch", [0x4F] = "XRecord", [0x50] = "Placeholder",
        [0x51] = "VbaProject", [0x52] = "Layout",
    };

    public static string? ResolveDisplayName(short type)
        => FixedDisplayNames.TryGetValue(type, out var n) ? n : null;
}
