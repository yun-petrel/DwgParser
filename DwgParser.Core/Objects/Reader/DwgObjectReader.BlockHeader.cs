using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    private sealed class BlockHeaderTail
    {
        public long End = -1;
        public long PreviewSize = -1;
        public int Count = -1;
        public string XrefPathName = "";
        public short NumInserts = -1;
        public string Description = "";
        public byte[]? Preview;
    }

    private const int MaxPreviewCaptureBytes = 1 << 20;

    private static bool TryWalkBlockHeaderTail(BitReader br, long anchor, out BlockHeaderTail tail)
    {
        tail = new BlockHeaderTail();
        long start = br.BitPosition;
        try
        {
            string xrefPname = br.ReadTV();
            int numIns = 0;
            while (true)
            {
                if (br.BitPosition + 8 > anchor) return WalkFail(br, start);
                if (br.ReadRC() == 0) break;
                if (++numIns > 0xF000) return WalkFail(br, start);
            }
            string description = br.ReadTV();
            long pv = br.ReadBL();
            if (pv < 0 || br.BitPosition > anchor) return WalkFail(br, start);
            if (pv > 0)
            {
                if (anchor - br.BitPosition != pv * 8) return WalkFail(br, start);
                byte[]? pvb = null;
                if (pv <= MaxPreviewCaptureBytes)
                {
                    pvb = new byte[pv];
                    for (int i = 0; i < pv; i++) pvb[i] = br.ReadRC();
                }
                tail.End = anchor; tail.PreviewSize = pv; tail.Count = numIns;
                tail.XrefPathName = xrefPname; tail.NumInserts = (short)numIns;
                tail.Description = description; tail.Preview = pvb;
                br.SeekBits(anchor);
                return true;
            }
            if (br.BitPosition == anchor)
            {
                tail.End = anchor; tail.PreviewSize = 0; tail.Count = numIns;
                tail.XrefPathName = xrefPname; tail.NumInserts = (short)numIns;
                tail.Description = description;
                return true;
            }
            return WalkFail(br, start);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                      or OverflowException or ArgumentException)
        {
            return WalkFail(br, start);
        }
    }

    private static bool WalkFail(BitReader br, long rewindTo)
    {
        br.SeekBits(rewindTo);
        return false;
    }

    private static bool DecodeBlockHeader(BitReader br, byte[] data, int objStart, ulong handle,
        WalkResult result, DwgVersion version, DwgObjectRecord? record)
    {
        var region = new BlockRegion { HeaderHandle = handle, EntityListStart = result.Entities.Count };

        int scanEnd = Math.Min(objStart + 512, data.Length);
        for (int i = objStart; i < scanEnd - 2; i++)
        {
            byte b = data[i];
            if (b == '*' || (char)b == '_')
            {
                int j = i;
                while (j < scanEnd && IsNameByte(data[j]))
                    j++;
                if (j - i >= 4)
                {
                    region.NameGuess = Encoding.ASCII.GetString(data, i, j - i);
                    break;
                }
            }
        }
        result.BlockRegions.Add(region);
        result.BlockHeaderByHandle[handle] = region;

        if (version.IsR2000())
        {
            uint bhBitsize = 0;
            try
            {
                bhBitsize = br.ReadRL();
                br.ReadHandle();
                TrySkipEed(br);
                br.ReadBL();
                long nameStart = br.BitPosition;
                string name = br.ReadTV();
                int nameBits = (int)(br.BitPosition - nameStart);
                bool xrefRef15 = br.ReadB();
                short xrefRes15 = br.ReadBS();
                bool xrefDep15 = br.ReadB();
                bool anonymous = br.ReadB();
                region.Name = name;
                region.IsAnonymous = anonymous;
                region.IsAnonymousDimBlock = anonymous && name == "*D";
                region.IsXrefRef = xrefRef15;
                region.XrefResolved = xrefRes15;
                region.IsXrefDep = xrefDep15;

                if (version >= DwgVersion.AC1018)
                {
                    br.ReadB();
                    br.ReadBL();
                    var bp = br.Read3BD();
                    region.BasePoint = new Vec2(bp.X, bp.Y);
                    region.HasBasePoint = true;
                }

                if (record != null && nameBits > 0)
                {
                    bool hasattrs15 = br.ReadB();
                    bool blkisxref15 = br.ReadB();
                    bool xrefovl15 = br.ReadB();
                    bool xrefloaded15 = br.ReadB();
                    var bp15 = br.Read3BD();
                    string xrefPname15 = br.ReadTV();
                    int numIns15 = 0;
                    while (true)
                    {
                        if (br.ReadRC() == 0) break;
                        if (++numIns15 > 0xF000) throw new OverflowException();
                    }
                    string desc15 = br.ReadTV();
                    int psz15 = (int)br.ReadBL();
                    byte[]? pv15 = null;
                    if (psz15 > 0)
                    {
                        if (psz15 > 0xA00000) throw new OverflowException();
                        pv15 = new byte[psz15];
                        for (int i = 0; i < psz15; i++) pv15[i] = br.ReadRC();
                    }
                    long bhExpect15 = (long)objStart * 8 + bhBitsize;
                    long bhDelta15 = bhExpect15 - br.BitPosition;
                    if (bhBitsize > 0 && bhDelta15 >= 0 && bhDelta15 <= 8)
                    {
                        record.StringFields.Add(new DwgObjectRecord.StringField(nameStart, nameBits, name));
                        region.BasePoint = new Vec2(bp15.X, bp15.Y);
                        region.HasBasePoint = true;
                        region.HasAttrs = hasattrs15;
                        region.IsXref = blkisxref15;
                        region.IsXrefOverlaid = xrefovl15;
                        region.IsXrefLoaded = xrefloaded15;
                        region.XrefPathName = xrefPname15;
                        region.NumInserts = numIns15;
                        region.Description = desc15;
                        region.PreviewSize = psz15;
                        region.Preview = pv15;
                        region.FieldsCaptured = true;
                        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                            System.Console.Error.WriteLine(
                                $"BHCAP15 h={handle:X} stop={br.BitPosition} expect={bhExpect15} (Δ={bhDelta15}) 捕获 \"{name}\"");
                    }
                }
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                          or OverflowException or ArgumentException)
            {
            }
        }
        else if (version >= DwgVersion.AC1018 && version < DwgVersion.AC1021)
        {
            try
            {
                uint bhBitsize = br.ReadRL();
                br.ReadHandle();
                long bhEedStart = br.BitPosition;
                TrySkipEed(br);
                if (record != null)
                {
                    record.EedStartBit = bhEedStart;
                    record.EedBitLen = (int)(br.BitPosition - bhEedStart);
                }
                br.ReadBL();
                br.ReadB();
                long nameStart = br.BitPosition;
                string name = br.ReadTV();
                int nameBits = (int)(br.BitPosition - nameStart);
                bool xrefRef18 = br.ReadB();
                short xrefRes18 = br.ReadBS();
                bool xrefDep18 = br.ReadB();
                bool anonymous = br.ReadB();
                bool hasattrs18 = br.ReadB();
                bool blkisxref18 = br.ReadB();
                bool xrefovl18 = br.ReadB();
                bool xrefloaded18 = br.ReadB();
                int numOwned18 = br.ReadBL();
                var bp = br.Read3BD();
                long bhAnchor = (long)objStart * 8 + bhBitsize;
                long bhBaseEnd = br.BitPosition;
                bool bhTailOk;
                long bhPreview = 0;
                bhTailOk = TryWalkBlockHeaderTail(br, bhAnchor, out BlockHeaderTail bhTail);
                if (bhTailOk)
                {
                    bhPreview = bhTail.PreviewSize;
                    if (record != null) record.BlockHeaderInsertCount = bhTail.Count;
                }
                if (!bhTailOk) br.SeekBits(bhBaseEnd);
                region.Name = name;
                region.IsAnonymous = anonymous;
                region.IsAnonymousDimBlock = anonymous && name == "*D";
                region.BasePoint = new Vec2(bp.X, bp.Y);
                region.HasBasePoint = true;
                region.IsXrefRef = xrefRef18;
                region.XrefResolved = xrefRes18;
                region.IsXrefDep = xrefDep18;
                region.HasAttrs = hasattrs18;
                region.IsXref = blkisxref18;
                region.IsXrefOverlaid = xrefovl18;
                region.IsXrefLoaded = xrefloaded18;
                region.NumOwned = numOwned18;
                if (record != null)
                {
                    long hsLocal = (long)objStart * 8 + bhBitsize;
                    record.BlockOwnedHandles =
                        TryReadOwnedVectorR2004(data, hsLocal, (long)record.BodyEnd * 8, numOwned18);
                    record.BlockOwnedSourceCount = numOwned18;
                }
                if (bhTailOk)
                {
                    region.XrefPathName = bhTail.XrefPathName;
                    region.NumInserts = bhTail.NumInserts;
                    region.Description = bhTail.Description;
                    region.PreviewSize = (int)bhTail.PreviewSize;
                    region.Preview = bhTail.Preview;
                }
                long bhDelta = bhAnchor - br.BitPosition;
                bool bhClosed = bhTailOk && bhDelta == 0;
                if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                    System.Console.Error.WriteLine(
                        $"BHCAP h={handle:X} stop={br.BitPosition} baseEnd={bhBaseEnd} expect={bhAnchor} "
                        + $"pv={bhPreview} tailok={(bhTailOk ? 1 : 0)} closed={(bhClosed ? 1 : 0)} name=\"{name}\"");
                if (record != null && bhBitsize > 0 && nameBits > 0
                    && (bhClosed || (bhDelta >= 0 && bhDelta <= 32)))
                {
                    record.StringFields.Add(new DwgObjectRecord.StringField(nameStart, nameBits, name));
                    if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                        System.Console.Error.WriteLine($"BHCAP h={handle:X} 捕获 \"{name}\" (Δ={bhDelta})");
                }
                if (bhClosed) region.FieldsCaptured = true;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                          or OverflowException or ArgumentException)
            {
            }
        }
        else if (version >= DwgVersion.AC1021)
        {
            bool r2004Plus = version.IsR2004Plus();
            bool r2013Plus = version >= DwgVersion.AC1027;
            bool ac1021Hybrid = version == DwgVersion.AC1021;
            if (ac1021Hybrid) AttachStringStreamAc1021(br, objStart, record);
            try
            {
                if (ac1021Hybrid) br.ReadRL();
                br.ReadHandle();
                TrySkipEed(br);
                br.ReadBL();
                if (r2004Plus) br.ReadB();
                if (r2013Plus) br.ReadB();
                string name = br.ReadTV();
                short xrefRes21 = br.ReadBS();
                bool anonymous = br.ReadB();
                bool hasattrs21 = br.ReadB();
                bool blkisxref21 = br.ReadB();
                bool xrefovl21 = br.ReadB();
                bool xrefloaded21 = br.ReadB();
                int numOwned21 = br.ReadBL();
                var bp = br.Read3BD();
                if (!string.IsNullOrEmpty(name))
                {
                    region.Name = name;
                    region.IsAnonymous = anonymous;
                    region.IsAnonymousDimBlock = anonymous && name == "*D";
                    region.BasePoint = new Vec2(bp.X, bp.Y);
                    region.HasBasePoint = true;
                    region.XrefResolved = xrefRes21;
                    region.HasAttrs = hasattrs21;
                    region.IsXref = blkisxref21;
                    region.IsXrefOverlaid = xrefovl21;
                    region.IsXrefLoaded = xrefloaded21;
                    region.NumOwned = numOwned21;
                    region.FieldsCaptured = true;
                    if (record is { HandleStreamStartBit: > 0 } rec21)
                    {
                        record.BlockOwnedHandles = TryReadOwnedVectorR2004(
                            data, rec21.HandleStreamStartBit, (long)rec21.BodyEnd * 8, numOwned21);
                        record.BlockOwnedSourceCount = numOwned21;
                    }
                    if (record is { HandleStreamStartBit: > 0 } recBh)
                    {
                        long tailStart21 = br.BitPosition;
                        try
                        {
                            short numIns21 = br.ReadRC();
                            int psz21 = (int)br.ReadBL();
                            if (psz21 < 0) throw new EndOfStreamException();
                            byte[]? pv21 = null;
                            if (psz21 > 0)
                            {
                                if (psz21 > 0xA00000) throw new OverflowException();
                                if (psz21 <= MaxPreviewCaptureBytes)
                                {
                                    pv21 = new byte[psz21];
                                    for (int i = 0; i < psz21; i++) pv21[i] = br.ReadRC();
                                }
                                else
                                {
                                    br.SeekBits(br.BitPosition + psz21 * 8);
                                }
                            }
                            short units21 = br.ReadBS();
                            bool expl21 = br.ReadB();
                            byte scaling21 = br.ReadRC();
                            if (br.BitPosition == recBh.HandleStreamStartBit)
                            {
                                region.NumInserts = numIns21;
                                region.PreviewSize = psz21;
                                region.Preview = pv21;
                                region.InsertUnits = units21;
                                region.Explodable = expl21;
                                region.BlockScaling = scaling21;
                            }
                        }
                        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                                      or OverflowException or ArgumentException)
                        {
                        }
                        finally
                        {
                            br.SeekBits(tailStart21);
                        }
                    }
                }
                if (DwgObjectStreamWriter.DebugTrace && !string.IsNullOrEmpty(name))
                    System.Console.Error.WriteLine($"DWGDBG BLKH h={handle:X} name=\"{name}\"");
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                          or OverflowException or ArgumentException)
            {
            }
        }
        return true;
    }

    private static void AttachStringStreamAc1021(BitReader br, int objStart, DwgObjectRecord? record)
    {
        try
        {
            long save = br.BitPosition;
            uint bitsize = br.ReadRL();
            br.SeekBits(save);
            if (bitsize == 0) return;
            long hs = (long)objStart * 8 + bitsize;
            if (record != null && hs > 0 && hs < (long)br.UnderlyingData().Length * 8)
            {
                record.HandleStreamStartBit = hs;
                AttachStringStream(br, objStart, bitsize, DwgVersion.AC1021, record);
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                      or OverflowException or ArgumentException)
        {
        }
    }
}
