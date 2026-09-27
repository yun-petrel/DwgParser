using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    private const short TypeText = 0x01;
    private const short TypeAttrib = 0x02;
    private const short TypeAttDef = 0x03;
    private const short TypeBlockEntity = 0x04;
    private const short TypeEndBlk = 0x05;
    private const short TypeSeqEnd = 0x06;
    private const short TypeInsert = 0x07;
    private const short TypeMInsert = 0x08;
    private const short TypeVertex2D = 0x0A;
    private const short TypePolyline2D = 0x0F;
    private const short TypeArc = 0x11;
    private const short TypeCircle = 0x12;
    private const short TypeLine = 0x13;
    private const short TypePoint = 0x1B;
    private const short Type3dFace = 0x1C;
    private const short TypeSolid = 0x1F;
    private const short TypeTrace = 0x20;
    private const short TypeMText = 0x2C;
    private const short TypeBlockControl = 0x30;
    private const short TypeBlockHeader = 0x31;
    private const short TypeLayerControl = 0x32;
    private const short TypeLayer = 0x33;
    private const short TypeStyleControl = 0x34;
    private const short TypeStyle = 0x35;
    private const short TypeLType = 0x39;
    private const short TypeAppid = 0x43;
    private const short TypeVport = 0x41;
    private const short TypeScale = 506;
    private const short TypeDictWdflt = 500;
    private const short TypeCellStyleMap = 512;
    private const short TypeTableContent = 531;
    private const short TypeTableGeometry = 532;
    private const short TypeDataTable = 533;
    private const short TypeProxyObject = 0x1F3;
    private const short TypeDimStyle = 0x45;
    private const short TypeTableStyle = 0x1F8;
    private const short TypeSectionViewStyle = 510;
    private const short TypeDetailViewStyle = 511;
    private const short TypeMLeaderStyle = 507;
    private const short TypeVisualStyle = 509;
    private const short TypeDictionaryVar = 0x1F7;
    private const short TypeLwPolyline = 0x4D;

    private const short TypeDimOrdinate = 0x14;
    private const short TypeDimLinear = 0x15;
    private const short TypeDimAligned = 0x16;
    private const short TypeDimAng3pt = 0x17;
    private const short TypeDimAng2ln = 0x18;
    private const short TypeDimRadius = 0x19;
    private const short TypeDimDiameter = 0x1A;
    private const short TypeEllipse = 0x23;
    private const short TypeSpline = 0x24;
    private const short TypeRegion = 0x25;
    private const short TypeSolid3d = 0x26;
    private const short TypeBody = 0x27;
    private const short TypeRay = 0x28;
    private const short TypeXLine = 0x29;
    private const short TypeLeader = 0x2D;
    private const short TypeTolerance = 0x2E;
    private const short TypeMline = 0x2F;
    private const short TypeViewport = 0x22;
    private const short TypeTable = 0x212;
    private const short TypeMLeader = 0x216;
    private const short TypeDictionary = 0x2a;
    private const short TypeHatch = 0x4E;
}
