using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public enum DwgObjectRecordKind
{
    Entity,
    TableControl,

    Opaque,
}

public sealed partial class DwgObjectRecord
{
    public ulong Handle;

    public short TypeCode;

    public string? ClassName;

    private short? _logicalType;

    public short LogicalType
    {
        get => _logicalType ?? TypeCode;
        set => _logicalType = value;
    }

    public DwgObjectRecordKind Kind = DwgObjectRecordKind.Opaque;

    public int BodyStart;
    public int BodyEnd;
    public uint SizeBytes;
    public int FrameEnd;
    public long TypeEndBit;

    public DwgVersion SourceVersion;

    public DwgHandleReference OwnHandle;

    public long CarryStartBit;
    public int CarryBitLen;

    public byte EntMode;
    public short ColorIndex;
    public long ColorStartBit;
    public int ColorBitLen;
    public double LinetypeScale;
    public readonly List<StringField> StringFields = new();

    public readonly record struct StringField(long Start, int BitLen, string Value);

    public readonly record struct EedField(
        long StartBit,
        int BitLen,
        int PayloadSize,
        DwgHandleReference ApplicationHandle,
        byte[] Payload);

    public readonly record struct EncColorField(
        byte Flags,
        bool HasAlpha,
        uint Alpha,
        bool HasDirectColorHandle,
        DwgHandleReference DirectColorHandle,
        bool HasTrueColor,
        uint TrueColor,
        string ColorName,
        string ColorBookName);

    public void CaptureString(long startBit, long endBit, string value)
    {
        int len = (int)(endBit - startBit);
        if (len >= 0) StringFields.Add(new StringField(startBit, len, value ?? string.Empty));
    }


    public ulong OwnerHandle;
    public ulong LayerHandle;
    public ulong BlockHeaderHandle;

    public long EedStartBit;
    public int EedBitLen;
    public readonly List<EedField> EedFields = new();
    public EncColorField? EncColor;
    public bool HasPreview;
    public ulong PreviewSize;
    public long PreviewPayloadStartBit;
    public int PreviewPayloadBitLen;
    public uint NumReactors;
    public bool NoLinks = true;
    public bool IsXdicMissing;
    public bool HasDsData;
    public short Invisible;
    public byte LineWeight;
    public byte LtypeFlags, PlotStyleFlags, MaterialFlags, ShadowFlags;
    public bool HasFullVisualStyle, HasFaceVisualStyle, HasEdgeVisualStyle;
    public DwgHandleReference OwnerRef;
    public readonly List<DwgHandleReference> ReactorHandles = new();
    public bool HasXdicSlot;
    public DwgHandleReference XdicRef;
    public bool HasLinks;
    public DwgHandleReference PrevRef, NextRef;
    public DwgHandleReference LayerRef;
    public bool HasLtypeSlot;
    public DwgHandleReference LtypeRef;
    public bool HasMaterialSlot;
    public DwgHandleReference MaterialRef;
    public bool HasShadowSlot;
    public DwgHandleReference ShadowRef;
    public bool HasPlotStyleSlot;
    public DwgHandleReference PlotStyleRef;
    public DwgHandleReference FullVsRef, FaceVsRef, EdgeVsRef;
    public DwgHandleReference BlockHeaderRef;
    public DwgHandleReference DimStyleRef;
    public DwgHandleReference DimBlockRef;
    public DwgHandleReference LeaderDimStyleRef;

    public bool HasLeaderDimStyleSlot;
    public bool HasStyleSlot;
    public DwgHandleReference StyleRef;
    public bool HasAttribs;
    public uint NumOwned;
    public bool HasAttribPair;
    public DwgHandleReference FirstAttribRef, LastAttribRef;
    public readonly List<DwgHandleReference> AttribRefs = new();
    public bool HasSeqEndSlot;
    public DwgHandleReference SeqEndRef;
    public long InsertAfterAttribsBit = -1;
    public int InsertNumOwnedBitLen;

    public int BlockHeaderInsertCount = -1;

    public long AttTailLockedBit = -1;

    public long AttTailMTextBit = -1;

    public long AttTailKeepDuplicateBit = -1;

    public int AttRoundTripLockedInBlock = -1;

    public int AttRoundTripMTextType = -1;

    public int AttRoundTripKeepDuplicateRecords = -1;

    public long DimActMeasurementEndBit = -1;

    public long DimUserTextSlotBit = -1;

    public string DimUserText = string.Empty;

    public long TableCellsStartBit = -1;

    public long TableCellsEndBit = -1;

    public bool WroteCentralStringStream;

    public long HatchBoundaryHandleTotal;
    public readonly List<DwgHandleReference> HatchBoundaryRefs = new();

    public bool HasVportHeaderSlot;
    public DwgHandleReference VportHeaderRef;
    public readonly List<DwgHandleReference> VportFrozenLayerRefs = new();
    public DwgHandleReference VportClipRef, VportNamedUcsRef, VportBaseUcsRef;
    public bool HasVportR2007Tail;
    public DwgHandleReference VportBackgroundRef, VportVisualStyleRef, VportShadePlotRef, VportSunRef;

    public bool VportR2007aTailSlot;

    public readonly List<DwgHandleReference> MLeaderTypeAxisRefs = new();

    public int MLeaderDownDropTypeAxisRefs;
    public int MLeaderUpAddTypeAxisRefs;

    public long HandleWalkEndBit;

    public bool HasSyntheticFrame;
    public SyntheticFrame? Synthetic;

    public long GeoStartBit;
    public int GeoBitLen;
    public long HandleStreamStartBit;
    public long StringStreamStartBit;
    public bool HasHandleStream;

    public bool HasOwnerSlot => EntMode == 0;

    public long HatchGradientEndBit = -1;

    public long HatchPatternSlotBit = -1;

    public System.Collections.Generic.List<(long Start, int Bits)>? HatchR2010SplineSpans;

    public string HatchPatternName = string.Empty;

    public string HatchGradientName = string.Empty;

    public int DictionaryNumItems = -1;
    public int DictionaryCloning;
    public int DictionaryIsHardOwner;
    public readonly List<(string Key, ulong ValueHandle)> DictionaryEntries = new();

    public bool HasMTextPrefix;
    public (double X, double Y, double Z) MTextInsPt;
    public (double X, double Y, double Z) MTextExtrusion;
    public (double X, double Y, double Z) MTextXAxisDir;
    public double MTextRectWidth;
    public double MTextRectHeight;
    public double MTextTextHeight;
    public short MTextAttachment;
    public short MTextFlowDir;
    public double MTextExtentsHeight;
    public double MTextExtentsWidth;
    public long MTextTailStartBit;
    public int MTextTailBitLen;
    public long MTextAnnotativeStartBit = -1;
    public long MTextAnnotativeEndBit = -1;

    public long LeaderTailInsertBit;

    public long LeaderBoxDeleteStartBit;
    public int LeaderBoxDeleteBitLen;


    public bool IsPseudoEntity => TypeCode is 0x04 or 0x05 or 0x06;

    public bool CanReencodeEntity =>
        Kind == DwgObjectRecordKind.Entity && GeoStartBit >= 0
        && (GeoBitLen > 0 || IsPseudoEntity);

    public BitReader OpenCarryReader(byte[] data)
    {
        var br = new BitReader(data, 0);
        SeekTo(br, CarryStartBit);
        return br;
    }

    internal static void SeekTo(BitReader br, long absoluteBit)
    {
        br.SetPosition((int)(absoluteBit / 8), (int)(absoluteBit % 8));
    }
}
