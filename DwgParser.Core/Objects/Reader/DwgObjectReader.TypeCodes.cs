using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    internal const short TypeText = 0x01;
    internal const short TypeAttrib = 0x02;
    internal const short TypeAttDef = 0x03;
    internal const short TypeBlockEntity = 0x04;
    internal const short TypeEndBlk = 0x05;
    internal const short TypeSeqEnd = 0x06;
    internal const short TypeInsert = 0x07;
    internal const short TypeMInsert = 0x08;
    internal const short TypeVertex2D = 0x0A;
    internal const short TypePolyline2D = 0x0F;
    internal const short TypeArc = 0x11;
    internal const short TypeCircle = 0x12;
    internal const short TypeLine = 0x13;
    internal const short TypePoint = 0x1B;
    internal const short Type3dFace = 0x1C;
    internal const short TypeSolid = 0x1F;
    internal const short TypeTrace = 0x20;
    internal const short TypeMText = 0x2C;
    internal const short TypeBlockControl = 0x30;
    internal const short TypeBlockHeader = 0x31;
    internal const short TypeLayerControl = 0x32;
    internal const short TypeLayer = 0x33;
    internal const short TypeStyleControl = 0x34;
    internal const short TypeStyle = 0x35;
    internal const short TypeLType = 0x39;
    internal const short TypeAppid = 0x43;
    internal const short TypeVport = 0x41;
    internal const short TypeScale = 506;
    internal const short TypeDictWdflt = 500;
    internal const short TypeCellStyleMap = 512;
    internal const short TypeTableContent = 531;
    internal const short TypeTableGeometry = 532;
    internal const short TypeDataTable = 533;
    internal const short TypeProxyObject = 0x1F3;
    internal const short TypeDimStyle = 0x45;
    internal const short TypeTableStyle = 0x1F8;
    internal const short TypeSectionViewStyle = 510;
    internal const short TypeDetailViewStyle = 511;
    internal const short TypeMLeaderStyle = 507;
    internal const short TypeVisualStyle = 509;
    internal const short TypeDictionaryVar = 0x1F7;
    internal const short TypeLwPolyline = 0x4D;

    internal const short TypeDimOrdinate = 0x14;
    internal const short TypeDimLinear = 0x15;
    internal const short TypeDimAligned = 0x16;
    internal const short TypeDimAng3pt = 0x17;
    internal const short TypeDimAng2ln = 0x18;
    internal const short TypeDimRadius = 0x19;
    internal const short TypeDimDiameter = 0x1A;
    internal const short TypeEllipse = 0x23;
    internal const short TypeSpline = 0x24;
    internal const short TypeRegion = 0x25;
    internal const short TypeSolid3d = 0x26;
    internal const short TypeBody = 0x27;
    internal const short TypeRay = 0x28;
    internal const short TypeXLine = 0x29;
    internal const short TypeLeader = 0x2D;
    internal const short TypeTolerance = 0x2E;
    internal const short TypeMline = 0x2F;
    internal const short TypeViewport = 0x22;
    internal const short TypeTable = 0x212;
    internal const short TypeMLeader = 0x216;
    internal const short TypeDictionary = 0x2a;
    internal const short TypeHatch = 0x4E;
}
