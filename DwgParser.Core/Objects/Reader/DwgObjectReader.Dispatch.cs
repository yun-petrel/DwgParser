using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    private const short TypeImageDef = 543;

    private const short TypeDimAssoc = 518;
    private const short TypeMLeaderObjectContextData = 567;
    private const short TypeDataLink = 565;

    private const short TypeLayout = 0x52;

    private static bool DecodeObject(byte[] data, int offset, ulong handle, WalkResult result,
        DwgVersion version, ClassTypeCodes.TypeNameResolver? typeName = null,
        System.Text.Encoding? legacyEncoding = null)
    {
        bool r2000 = version.IsR2000();
        bool r2010Plus = version.IsR2010Plus();
        int pos = offset;
        uint size = ReadMsRaw(data, ref pos);
        if (size == 0)
            return false;
        int sizeStart = pos;
        int frameEnd;
        if (r2010Plus)
        {
            int probe = pos;
            ReadUmcRaw(data, ref probe);
            frameEnd = probe + (int)size + 2;
        }
        else
        {
            frameEnd = pos + (int)size + 2;
        }
        int objEnd = Math.Min(frameEnd - 2, data.Length);
        if (objEnd <= pos)
            return false;

        var record = new DwgObjectRecord
        {
            Handle = handle,
            SourceVersion = version,
            BodyStart = sizeStart,
            BodyEnd = objEnd,
            SizeBytes = r2010Plus ? size : (uint)(objEnd - sizeStart),
        };
        record.FrameEnd = frameEnd;
        result.Records.Add(record);
        result.FrameEnds[record] = frameEnd;

        var br = new BitReader(data, pos)
        {
            UnicodeStrings = version >= DwgVersion.AC1021,
            LegacyEncoding = legacyEncoding,
        };
        int objStart = pos;
        uint bitsize;
        if (r2010Plus)
        {
            uint handlestreamSize = br.ReadUMC();
            objStart = br.BytePosition;
            bitsize = size * 8 - handlestreamSize;
        }
        else
        {
            bitsize = 0;
        }
        if (r2010Plus)
        {
            record.HandleStreamStartBit = (long)objStart * 8 + bitsize;
            record.HasHandleStream = true;
            AttachStringStream(br, objStart, bitsize, version, record);
        }

        short type = r2010Plus ? br.ReadBOT() : br.ReadBS();
        record.TypeCode = type;
        record.ClassName = type >= 500 ? typeName?.Invoke(type) : null;
        short logical = ClassTypeCodes.Canonical(type, record.ClassName);
        record.LogicalType = logical;
        record.TypeEndBit = br.BitPosition;
        record.CarryStartBit = br.BitPosition;
        record.CarryBitLen = objEnd * 8 - (int)br.BitPosition;
        record.Kind = ClassifyKind(logical);
        result.CountType(type);
        switch (logical)
        {
            case TypeLine:
            case TypeCircle:
            case TypeArc:
            case TypePoint:
            case TypeText:
            case TypeAttrib:
            case TypeAttDef:
            case TypeSolid:
            case Type3dFace:
            case TypeTrace:
            case TypeMText:
            case TypeInsert:
            case TypeMInsert:
            case TypeLwPolyline:
            case TypePolyline2D:
            case TypeVertex2D:
            case TypeEllipse:
            case TypeSpline:
            case TypeRay:
            case TypeXLine:
            case TypeLeader:
            case TypeTolerance:
            case TypeMline:
            case TypeRegion:
            case TypeSolid3d:
            case TypeBody:
            case TypeHatch:
            case TypeViewport:
            case TypeTable:
            case TypeMLeader:
            case TypeBlockEntity:
            case TypeEndBlk:
            case TypeSeqEnd:
            case TypeDimOrdinate:
            case TypeDimLinear:
            case TypeDimAligned:
            case TypeDimAng3pt:
            case TypeDimAng2ln:
            case TypeDimRadius:
            case TypeDimDiameter:
                return DecodeEntity(br, logical, handle, objEnd, objStart, result, version,
                    r2010Plus ? bitsize : null, record);
            case TypeBlockControl:
            {
                bool decoded = DecodeBlockControl(br, result, r2010Plus, objStart, version, record);
                TryDecodeTableControlAtCarryStart(br, handle, version, record, objStart);
                return FinishTable(decoded, record);
            }
            case TypeBlockHeader:
                return FinishTable(DecodeBlockHeader(br, data, objStart, handle, result, version, record), record);
            case TypeLayerControl:
            {
                bool decoded = DecodeLayerControl(br, result, r2010Plus);
                TryDecodeTableControlAtCarryStart(br, handle, version, record, objStart);
                return FinishTable(decoded, record);
            }
            case TypeLayer:
                return FinishTable(ObjectFactory.Decode(
                    ObjectFactory.CreateLayer(handle, version, record, result, objStart), br), record);
            case TypeStyle:
                try { DecodeStyle(br, handle, result, version, record, objStart); }
                catch {  }
                StampSilentReject(br, record, record?.Style);
                return false;
            case TypeLType:
                try { DecodeLType(br, handle, version, record, objStart); }
                catch {  }
                if (record?.LType is { ValuesCommitted: true } ltc)
                {
                    var lti = new LinetypeInfo
                    {
                        Handle = handle,
                        SourceVersion = version,
                        Name = ltc.Name,
                        Description = ltc.Description,
                        XrefResolved = ltc.XrefResolved,
                        NumDashes = ltc.NumDashes,
                        PatternLen = ltc.PatternLen,
                        Alignment = ltc.Alignment,
                    };
                    lti.Dashes.AddRange(ltc.Dashes);
                    result.LtypeStyles[handle] = lti;
                }
                return false;
            case TypeDimStyle:
                try { DecodeDimStyle(br, handle, result, version, record, objStart); }
                catch {  }
                return false;
            case TypeTableStyle:
                try
                {
                    if (record != null)
                    {
                        var ts = ObjectFactory.CreateTableStyle(handle, version, record, objStart);
                        if (ts != null) ObjectFactory.Decode(ts, br);
                    }
                }
                catch {  }
                StampSilentReject(br, record, record?.TableStyle);
                return false;
            case TypeSectionViewStyle:
            case TypeDetailViewStyle:
                try
                {
                    if (record != null)
                    {
                        var vtm = ObjectFactory.CreateViewStyleTemplate(handle, version, record, objStart);
                        if (vtm != null) ObjectFactory.Decode(vtm, br);
                    }
                }
                catch {  }
                try
                {
                    if (record != null)
                    {
                        var vsup = ObjectFactory.CreateViewStyle(handle, version, record, objStart);
                        if (vsup != null) ObjectFactory.Decode(vsup, br);
                    }
                }
                catch {  }
                StampSilentReject(br, record, record?.ViewStyle);
                return false;
            case TypeImageDef:
                try
                {
                    if (record != null)
                    {
                        var imd = ObjectFactory.CreateImageDef(handle, version, record, objStart);
                        if (imd != null) ObjectFactory.Decode(imd, br);
                    }
                }
                catch {  }
                StampSilentReject(br, record, record?.ImageDef);
                return false;
            case TypeDataLink:
                try { DecodeDataLink(br, version, record, objStart, r2010Plus); }
                catch {  }
                StampSilentReject(br, record, record?.DataLink);
                return false;
            case TypeDimAssoc:
                try { DecodeDimAssoc(br, version, record, objStart, r2010Plus); }
                catch {  }
                return false;
            case TypeMaterial:
                try
                {
                    if (record != null)
                    {
                        var mat = ObjectFactory.CreateMaterial(handle, version, record, objStart);
                        if (mat != null) ObjectFactory.Decode(mat, br);
                    }
                }
                catch {  }
                return false;
            case TypeLayout:
                try
                {
                    if (record != null)
                    {
                        var lay = ObjectFactory.CreateLayout(handle, version, record, objStart);
                        if (lay != null) ObjectFactory.Decode(lay, br);
                    }
                }
                catch {  }
                return false;
            case TypeMLeaderStyle:
                try
                {
                    if (record != null)
                    {
                        var mls = ObjectFactory.CreateMLeaderStyle(handle, version, record, objStart);
                        if (mls != null) ObjectFactory.Decode(mls, br);
                    }
                }
                catch {  }
                StampSilentReject(br, record, record?.MLeader);
                return false;
            case TypeVisualStyle:
                try
                {
                    if (record != null)
                    {
                        var vs = ObjectFactory.CreateVisualStyle(handle, version, record, objStart);
                        if (vs != null) ObjectFactory.Decode(vs, br);
                    }
                }
                catch {  }
                StampSilentReject(br, record, record?.VisualStyle);
                return false;
            case TypeDictionaryVar:
                try
                {
                    if (record != null)
                    {
                        var dv = ObjectFactory.CreateDictVar(handle, version, record, objStart);
                        if (dv != null) ObjectFactory.Decode(dv, br);
                    }
                }
                catch {  }
                return false;
            case TypeAppid:
            case TypeScale:
            case TypeVport:
                try { DecodeTrailingNameRecord(br, version, record, objStart, r2010Plus); }
                catch {  }
                if (record != null && version >= DwgVersion.AC1021
                    && (logical == TypeScale || logical == TypeAppid || logical == TypeVport))
                {
                    long savedBit = br.BitPosition;
                    DwgObjectRecord.SeekTo(br, record.CarryStartBit);
                    try
                    {
                        if (logical == TypeScale)
                        {
                            var sc = ObjectFactory.CreateScale(handle, version, record, objStart);
                            if (sc != null) ObjectFactory.Decode(sc, br);
                        }
                        else if (logical == TypeAppid)
                        {
                            var ap = ObjectFactory.CreateAppid(handle, version, record, objStart);
                            if (ap != null) ObjectFactory.Decode(ap, br);
                        }
                        else
                        {
                            var vp = ObjectFactory.CreateVport(handle, version, record, objStart);
                            if (vp != null) ObjectFactory.Decode(vp, br);
                        }
                    }
                    catch {  }
                    br.SetPosition((int)(savedBit >> 3), (int)(savedBit & 7));
                }
                if (record != null && version < DwgVersion.AC1021
                    && (logical == TypeAppid || logical == TypeScale))
                {
                    long savedBit2 = br.BitPosition;
                    try
                    {
                        if (logical == TypeAppid)
                        {
                            var ap2 = ObjectFactory.CreateAppid(handle, version, record, objStart);
                            if (ap2 != null) ObjectFactory.Decode(ap2, br);
                        }
                        else
                        {
                            var sc = ObjectFactory.CreateScale(handle, version, record, objStart);
                            if (sc != null) ObjectFactory.Decode(sc, br);
                        }
                    }
                    catch {  }
                    br.SetPosition((int)(savedBit2 >> 3), (int)(savedBit2 & 7));
                }
                try
                {
                    if (record != null && version < DwgVersion.AC1021
                        && record.LogicalType == TypeVport)
                    {
                        var vp = ObjectFactory.CreateVport(handle, version, record, objStart);
                        if (vp != null) ObjectFactory.Decode(vp, br);
                    }
                }
                catch {  }
                if (record != null && record.LogicalType == TypeVport)
                {
                    record.EedStartBit = 0;
                    record.EedBitLen = 0;
                }
                return false;
            case TypeMLineStyle:
                try
                {
                    if (record != null)
                    {
                        var mls = ObjectFactory.CreateMLineStyle(handle, version, record, objStart);
                        if (mls != null) ObjectFactory.Decode(mls, br);
                    }
                }
                catch {  }
                return false;
            case TypeDictWdflt:
                try { DecodeDictWdflt(br, handle, version, record, objStart); }
                catch {  }
                StampSilentReject(br, record, record?.DictWdflt);
                return false;
            case TypeProxyObject:
                try
                {
                    var px = ObjectFactory.CreateProxyObject(handle, version, record, objStart,
                        result.Classes);
                    if (px != null) ObjectFactory.Decode(px, br);
                }
                catch {  }
                StampSilentReject(br, record, record?.ProxyObject);
                return false;
            case TypeTableContent:
                try
                {
                    DecodeTableContentStructure(br, version, record, objStart, r2010Plus);
                }
                catch {  }
                try
                {
                    TryDecodeTableContentValue(br, handle, version, record, objStart);
                }
                catch {  }
                StampSilentReject(br, record, record?.TableContent);
                return false;
            case TypeDataTable:
                DecodeDataTableBranch(br, version, record);
                return false;
            case TypeCellStyleMap:
                try { DecodeCellStyleMapStrings(br, version, record, objStart); }
                catch {  }
                StampSilentReject(br, record, record?.CellStyleMap);
                return false;
            case TypeXrecord:
                try { DecodeXrecord(br, version, record); }
                catch {  }
                return false;
            case TypeDictionary:
                try { DecodeDictionary(br, r2010Plus, version, record); }
                catch {  }
                return false;
            case TypeStyleControl:
            case TypeLtypeControl:
            case TypeViewControl:
            case TypeUcsControl:
            case TypeVportControl:
            case TypeAppidControl:
            case TypeDimStyleControl:
            case TypeVxControl:
                try { TryDecodeTableControl(br, handle, version, record, objStart); }
                catch {  }
                return false;
            case TypeVxTableRecord:
                try { TryDecodeVxTableRecord(br, version, record, legacyEncoding); }
                catch {  }
                return false;
            case TypeAssocDependency:
            case TypeAssocGeomDependency:
                try { TryDecodeAssocDependency(br, version, record, legacyEncoding); }
                catch {  }
                return false;
            case TypeAssocNetwork:
            case TypeAssocAction:
                try { TryDecodeAssocAction(br, version, record, legacyEncoding); }
                catch {  }
                return false;
            case TypeMLeaderObjectContextData:
                try { TryDecodeMLeaderObjectContextData(br, version, record, legacyEncoding); }
                catch {  }
                return false;
            case TypeAssocVertexParam:
                try { TryDecodeAssocVertexParam(br, version, record, legacyEncoding); }
                catch {  }
                return false;
            case TypeSortentsTable:
            case TypeRasterVariables:
            case TypeSun:
            case TypeFieldList:
            case TypePdfDefinition:
            case TypeTableGeometry:
            case TypeField:
                try { TryDecodeSortRast(br, handle, version, record, objStart, logical); }
                catch {  }
                return false;
            default:
                try { TryDecodeRenderSettings(br, handle, version, record, objStart); }
                catch {  }
                try { TryDecodePurgePreventer(br, version, record); }
                catch {  }
                return false;
        }
    }

    internal static void StampSilentReject(BitReader br, DwgObjectRecord? rec, object? bag)
    {
        if (rec is null || bag is null) return;
        var t = bag.GetType();
        var why = t.GetField("FailReason", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var bit = t.GetField("FailBit", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (why is null || bit is null || why.FieldType != typeof(string) || bit.FieldType != typeof(long)) return;
        if (!string.IsNullOrEmpty((string?)why.GetValue(bag))) return;
        if (AnyAxisCommitted(bag)) return;
        int walked = (int)(br.BitPosition - rec.CarryStartBit);
        string reason = walked == 0
            ? "未走查(工厂出 null 或游标未动)"
            : $"静默拒收:走了{walked}位";
        why.SetValue(bag, reason);
        bit.SetValue(bag, (long)walked);
    }

    private static bool AnyAxisCommitted(object bag)
    {
        foreach (var f in bag.GetType().GetFields(System.Reflection.BindingFlags.Public
                                              | System.Reflection.BindingFlags.Instance))
            if (f.FieldType == typeof(bool)
                && (f.Name.EndsWith("Captured", StringComparison.Ordinal)
                    || f.Name.EndsWith("Committed", StringComparison.Ordinal))
                && (bool)f.GetValue(bag)!)
                return true;
        return false;
    }

    private static bool FinishTable(bool decoded, DwgObjectRecord record)
    {
        if (!decoded) record.Kind = DwgObjectRecordKind.Opaque;
        return decoded;
    }


    internal static void AttachStringStream(BitReader br, int objStart, uint bitsize, DwgVersion version, DwgObjectRecord record)
    {
        if (version < DwgVersion.AC1021 || bitsize < 34)
            return;
        byte[] data = br.UnderlyingData();
        long startBit = (long)objStart * 8 + bitsize - 1;
        if (startBit < 0 || startBit >= (long)data.Length * 8)
            return;
        var str = new BitReader(data) { UnicodeStrings = true };
        str.SetPosition((int)(startBit >> 3), (int)(startBit & 7));
        if (!str.ReadB())
            return;
        long rsPos = (long)objStart * 8 + bitsize - 17;
        str.SetPosition((int)(rsPos >> 3), (int)(rsPos & 7));
        uint dataSize = str.ReadRS();
        int sizeFieldBits = 17;
        if ((dataSize & 0x8000) != 0)
        {
            long hiPos = (long)objStart * 8 + bitsize - 33;
            str.SetPosition((int)(hiPos >> 3), (int)(hiPos & 7));
            dataSize = (dataSize & 0x7FFF) | ((uint)str.ReadRS() << 15);
            sizeFieldBits = 33;
        }
        long streamStart = dataSize < bitsize
            ? (long)objStart * 8 + bitsize - sizeFieldBits - dataSize
            : (long)objStart * 8;
        if (streamStart < 0 || streamStart >= (long)data.Length * 8)
            return;
        str.SetPosition((int)(streamStart >> 3), (int)(streamStart & 7));
        br.StringStream = str;
        record.StringStreamStartBit = streamStart;
    }
}