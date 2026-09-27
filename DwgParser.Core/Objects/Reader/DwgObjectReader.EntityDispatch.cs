using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static bool DecodeEntity(BitReader br, short type, ulong handle, int objEnd, int objStart,
        WalkResult result, DwgVersion version, uint? bitsizeOverride = null, DwgObjectRecord? record = null)
    {
        bool r2000 = version.IsR2000();
        bool r2007Plus = version >= DwgVersion.AC1021;
        bool r2010Plus = version.IsR2010Plus();
        bool r2018Plus = version >= DwgVersion.AC1032;
        DwgEntity entity =
            type == TypeLine ? EntityFactory.CreateLine(handle) :
            type == TypeTable ? EntityFactory.CreateTable(handle, r2010Plus) :
            type == TypeLwPolyline ? EntityFactory.CreateLwPolyline(handle) :
            (type == TypeText) ? EntityFactory.CreateText(handle) :
            type == TypeAttrib ? EntityFactory.CreateAttribute(handle, r2007Plus, r2010Plus, r2018Plus) :
            type == TypeAttDef ? EntityFactory.CreateAttDef(handle, r2007Plus, r2010Plus, r2018Plus) :
            type == TypeMText ? EntityFactory.CreateMText(handle, version) :
            (type == TypeInsert || type == TypeMInsert)
                ? EntityFactory.CreateInsert(handle, r2000) :
            type == TypeCircle ? EntityFactory.CreateCircle(handle) :
            type == TypeArc ? EntityFactory.CreateArc(handle) :
            type == TypeEllipse ? EntityFactory.CreateEllipse(handle) :
            type == TypePolyline2D ? EntityFactory.CreatePolyline2D(handle) :
            type == TypeVertex2D ? EntityFactory.CreateVertex2D(handle) :
            type == TypePoint ? EntityFactory.CreatePoint(handle) :
            type == TypeSpline ? EntityFactory.CreateSpline(handle, version) :
            type == TypeRay ? EntityFactory.CreateRay(handle) :
            type == TypeXLine ? EntityFactory.CreateXLine(handle) :
            type == TypeMLeader ? EntityFactory.CreateMLeader(handle, version) :
            type == TypeSolid ? EntityFactory.CreateSolid(handle) :
            type == TypeTrace ? EntityFactory.CreateTrace(handle) :
            type == Type3dFace ? EntityFactory.CreateFace(handle) :
            type == TypeLeader ? EntityFactory.CreateLeader(handle) :
            type == TypeMline ? EntityFactory.CreateMLine(handle) :
            type == TypeTolerance ? EntityFactory.CreateTolerance(handle, r2007Plus) :
            type == TypeRegion ? EntityFactory.CreateRegion(handle) :
            type == TypeSolid3d ? EntityFactory.CreateSolid3d(handle) :
            type == TypeBody ? EntityFactory.CreateBody(handle) :
            type == TypeBlockEntity ? EntityFactory.CreateBlock(handle) :
            type == TypeEndBlk ? EntityFactory.CreateEndBlk(handle) :
            type == TypeSeqEnd ? EntityFactory.CreateSeqEnd(handle) :
            type == TypeHatch ? EntityFactory.CreateHatch(handle, version) :
            type == TypeViewport ? EntityFactory.CreateViewport(handle, version) :
            type is >= TypeDimOrdinate and <= TypeDimDiameter
                ? EntityFactory.CreateDimension(handle, type) :
            new DwgEntity { Handle = handle };
        uint bitsize = bitsizeOverride ?? br.ReadRL();
        if (record != null)
        {
            record.HandleStreamStartBit = (long)objStart * 8 + bitsize;
            record.HasHandleStream = true;
            if (!r2010Plus && version >= DwgVersion.AC1021)
                AttachStringStream(br, objStart, bitsize, version, record);
            record.OwnHandle = br.ReadHandle();
            if (record.OwnHandle.Value != 0)
            {
                entity.Handle = record.OwnHandle.Value;
                if (record.Handle == 0) record.Handle = entity.Handle;
            }
        }
        else
        {
            var own = br.ReadHandle();
            if (own.Value != 0)
                entity.Handle = own.Value;
        }
        if (record != null)
            record.EedStartBit = br.BitPosition;
        if (!TryReadEed(br, record))
        {
            if (record != null)
            {
                record.EedFields.Clear();
                record.Kind = DwgObjectRecordKind.Opaque;
            }
            return false;
        }
        if (record != null)
            record.EedBitLen = (int)(br.BitPosition - record.EedStartBit);
        if (record != null)
            record.GeoStartBit = br.BitPosition;

        var ced = EntityFactory.CreateCommonEntityData(version);
        ced.Read(br, entity, record, bitsize, objStart, objEnd);
        bitsize = ced.Bitsize;
        byte entmode = ced.EntMode;
        double ltypeScale = ced.LinetypeScale;
        if (record != null)
        {
            record.GeoStartBit = br.BitPosition;
            if (DwgObjectStreamWriter.DebugTrace)
                System.Console.Error.WriteLine(
                    $"DBG h={record.Handle:X} t={record.TypeCode} v={version} geoStart={record.GeoStartBit} " +
                    $"commonEnd=@{br.BitPosition} entmode={entmode} nolinks={ced.NoLinks} isXdic={ced.IsXdicMissing} " +
                    $"hasDs={ced.HasDsData} colorBits={record.ColorBitLen} ltypeScale={ltypeScale:F2}");
        }

        bool hasAttribs = false;
        switch (type)
        {
            case TypeLine: EntityFactory.Decode((ILine)entity, br); break;
            case TypeCircle: EntityFactory.Decode((ICircle)entity, br); break;
            case TypeArc: EntityFactory.Decode((IArc)entity, br); break;
            case TypePoint: EntityFactory.Decode((IPoint)entity, br); break;
            case TypeText:
                EntityFactory.Decode((IText)entity, br, record);
                break;
            case TypeAttrib:
                EntityFactory.Decode((IAttribute)entity, br, record);
                break;
            case TypeAttDef:
                EntityFactory.Decode((IAttDef)entity, br, record);
                break;
            case TypeMText:
                EntityFactory.Decode((IMText)entity, br, record);
                break;
            case TypeSolid:
                EntityFactory.Decode((ISolid)entity, br);
                break;
            case TypeTrace:
                EntityFactory.Decode((ITrace)entity, br);
                break;
            case Type3dFace:
                EntityFactory.Decode((IFace)entity, br);
                break;
            case TypeInsert:
            case TypeMInsert:
                hasAttribs = EntityFactory.Decode((IInsert)entity, br, record);
                break;
            case TypeLwPolyline:
                EntityFactory.Decode((ILwPolyline)entity, br);
                break;
            case TypePolyline2D:
                EntityFactory.Decode((IPolyline2D)entity, br);
                break;
            case TypeVertex2D:
                EntityFactory.Decode((IVertex2D)entity, br);
                break;
            case TypeEllipse:
                EntityFactory.Decode((IEllipse)entity, br);
                break;
            case TypeRay:
                EntityFactory.Decode((IRay)entity, br);
                break;
            case TypeXLine:
                EntityFactory.Decode((IXLine)entity, br);
                break;
            case TypeLeader:
                EntityFactory.Decode((ILeader)entity, br, record);
                break;
            case TypeSpline:
                EntityFactory.Decode((ISpline)entity, br);
                break;
            case TypeHatch:
                EntityFactory.Decode((IHatch)entity, br, record);
                break;
            case TypeDimOrdinate:
            case TypeDimLinear:
            case TypeDimAligned:
            case TypeDimAng3pt:
            case TypeDimAng2ln:
            case TypeDimRadius:
            case TypeDimDiameter:
                {
                    List<DimFieldEntry>? tl = null;
                    if (TraceDimensions)
                    {
                        _dimTraces ??= new Dictionary<ulong, IReadOnlyList<DimFieldEntry>>();
                        tl = new List<DimFieldEntry>();
                        _dimTraces[record?.Handle ?? 0UL] = tl;
                    }
                    EntityFactory.Decode((IDimension)entity, br, record, tl, version);
                }
                break;
            case TypeViewport:
                EntityFactory.Decode((IViewport)entity, br);
                break;
            case TypeTable:
                EntityFactory.Decode((ITable)entity, br, version, record);
                break;
            case TypeMLeader:
                EntityFactory.Decode((IMLeader)entity, br, record);
                break;
            case TypeMline:
                EntityFactory.Decode((IMLine)entity, br, record, version, handle, objStart);
                break;
            case TypeTolerance:
                EntityFactory.Decode((ITolerance)entity, br, record, version, handle, objStart);
                break;
            case TypeRegion:
                EntityFactory.Decode((IRegion)entity, br, record);
                break;
            case TypeSolid3d:
                EntityFactory.Decode((ISolid3d)entity, br, record);
                break;
            case TypeBody:
                EntityFactory.Decode((IBody)entity, br, record);
                break;
            case TypeBlockEntity:
                EntityFactory.Decode((IBlock)entity, br, record, version, handle, objStart);
                break;
            case TypeEndBlk:
                EntityFactory.Decode((IEndBlk)entity, br);
                break;
            case TypeSeqEnd:
                EntityFactory.Decode((ISeqEnd)entity, br);
                break;
        }

        WalkEntityHandles(br.UnderlyingData(), entity, type, bitsize, objStart,
            ced, hasAttribs, version, record);

        if (record != null)
        {
            record.EntMode = entmode;
            record.ColorIndex = entity.ColorIndex;
            record.LinetypeScale = ltypeScale;
            record.OwnerHandle = entity.OwnerHandle;
            record.LayerHandle = entity.LayerHandle;
            record.BlockHeaderHandle = entity.BlockHeaderHandle;
            record.HasAttribs = hasAttribs;
            if (record.HasMTextPrefix && record.HasHandleStream
                && record.HandleStreamStartBit > record.MTextTailStartBit)
            {
                record.MTextTailBitLen =
                    (int)(record.HandleStreamStartBit - record.MTextTailStartBit);
            }
            int geoLen = (int)(record.HandleStreamStartBit - record.GeoStartBit);
            if (geoLen < 0 || record.GeoStartBit + geoLen > (long)objEnd * 8)
            {
                record.Kind = DwgObjectRecordKind.Opaque;
                record.GeoBitLen = 0;
            }
            else
            {
                record.GeoBitLen = geoLen;
            }
        }

        if (type is not (TypeBlockEntity or TypeEndBlk or TypeSeqEnd))
            result.Entities.Add(entity);
        if (type == TypeBlockEntity && entity is BlockEntity blockBegin)
            result.BlockBegins.Add(blockBegin);
        return true;
    }
}
