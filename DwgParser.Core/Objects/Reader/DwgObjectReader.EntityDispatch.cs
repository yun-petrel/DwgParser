using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    internal static List<DimFieldEntry>? BeginDimensionTrace(DwgObjectRecord? record)
    {
        if (!TraceDimensions) return null;
        _dimTraces ??= new Dictionary<ulong, IReadOnlyList<DimFieldEntry>>();
        var tl = new List<DimFieldEntry>();
        _dimTraces[record?.Handle ?? 0UL] = tl;
        return tl;
    }

    private static bool DecodeEntity(BitReader br, short type, ulong handle, int objEnd, int objStart,
        WalkResult result, DwgVersion version, uint? bitsizeOverride = null, DwgObjectRecord? record = null)
    {
        IEntityCodec codec = EntityCodecRegistry.Default.Resolve(type, version)
            ?? throw new NotSupportedException(
                $"实体类型 0x{type:X3}（{version}）未注册编解码器");
        DwgEntity entity = (DwgEntity)codec.Create(handle, type, version);
        uint bitsize = bitsizeOverride ?? br.ReadRL();
        if (record != null)
        {
            record.HandleStreamStartBit = (long)objStart * 8 + bitsize;
            record.HasHandleStream = true;
            if (!version.IsR2010Plus() && version >= DwgVersion.AC1021)
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

        bool hasAttribs = codec.Decode(entity, br, record,
            new EntityWindow(objStart, objEnd, bitsize, version, handle));

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
