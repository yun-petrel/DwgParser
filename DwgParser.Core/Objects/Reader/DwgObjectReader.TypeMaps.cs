using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    private static DwgObjectRecordKind ClassifyKind(short type) => type switch
    {
        TypeLine or TypeCircle or TypeArc or TypePoint or TypeText or TypeAttrib or TypeAttDef
            or TypeSolid or Type3dFace or TypeTrace or TypeMText or TypeInsert or TypeMInsert
            or TypeLwPolyline or TypePolyline2D or TypeVertex2D or TypeEllipse or TypeSpline
            or TypeRay or TypeXLine or TypeLeader or TypeTolerance or TypeMline
            or TypeRegion or TypeSolid3d or TypeBody or TypeHatch
            or TypeViewport or TypeDimOrdinate or TypeDimLinear or TypeDimAligned
            or TypeDimAng3pt or TypeDimAng2ln or TypeDimRadius or TypeDimDiameter
            or TypeTable or TypeMLeader
            or TypeBlockEntity or TypeEndBlk or TypeSeqEnd
            => DwgObjectRecordKind.Entity,
        TypeBlockControl or TypeBlockHeader or TypeLayerControl or TypeLayer
            => DwgObjectRecordKind.TableControl,
        _ => DwgObjectRecordKind.Opaque,
    };

    private static DwgEntityType MapTypeToKind(short type) => type switch
    {
        TypeSpline => DwgEntityType.Spline,
        TypeRegion => DwgEntityType.Region,
        TypeSolid3d => DwgEntityType.Solid3d,
        TypeBody => DwgEntityType.Body,
        TypeTolerance => DwgEntityType.Tolerance,
        TypeMline => DwgEntityType.MLine,
        TypeViewport => DwgEntityType.Viewport,
        TypeTable => DwgEntityType.Table,
        TypeMLeader => DwgEntityType.MLeader,
        TypeBlockEntity => DwgEntityType.Block,
        TypeEndBlk => DwgEntityType.EndBlk,
        TypeSeqEnd => DwgEntityType.SeqEnd,
        _ => DwgEntityType.Dimension,
    };
}