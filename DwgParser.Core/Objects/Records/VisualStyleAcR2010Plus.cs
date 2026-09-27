using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleAcR2010Plus : VisualStyleRecord
{
    public override string Family => "R2010Plus";

    private const int PropsBlockBits = 512;

    public VisualStyleAcR2010Plus(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    public override long BodyStartBit => Cap.DownBodyStartBit;
    public override long StreamStartBit => Cap.DownStreamStartBit;
    public override long[] FieldSpans => Cap.DownFieldSpans;

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion < DwgVersion.AC1024) return false;
        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        long hs = Rec.HandleStreamStartBit;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return false; }
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
        Cap.EedStartBit = eedStart;
        Cap.EedBitLen = (int)(br.BitPosition - eedStart);
        if (Cap.EedBitLen < 2) { Fail(br, $"EED 长度异常={Cap.EedBitLen}位"); return false; }
        int numReactors = br.ReadBL();
        br.ReadB();
        if (SrcVersion >= DwgVersion.AC1027) br.ReadB();
        if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return false; }
        long bodyStart = br.BitPosition;
        var scalars = new int[20];
        var reg = new VisualStyleRegion();
        var spans = new List<long>(18);
        double brightness = 0.0;
        int curSlot = -1;
        bool bdRangeOk = true;
        string? bdTag = null;
        int spanSeq = 0, cmcSeq = 0;
        try
        {
            int fIdx = 0;
            int S(int slot, Func<int> rd, int hi, bool hasInt = true)
            {
                curSlot = slot;
                int v = rd();
                if (v < 0 || v > hi) throw new InvalidDataException($"S{slot}={v}>{hi}");
                scalars[slot] = v;
                if (hasInt) reg.Ints176.Add(br.ReadBS());
                if (dbg) System.Console.Error.WriteLine($"  VSDf[{fIdx++}] S{slot}={v}");
                return v;
            }
            double Span(Func<double> rd, double lo, double hi)
            {
                curSlot = 100 + spanSeq++;
                long p0 = br.BitPosition;
                double v = rd();
                if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12)
                    throw new InvalidDataException($"D={v:R}");
                if (v < lo || v > hi) { bdRangeOk = false; bdTag ??= $"slot{curSlot}"; }
                spans.Add(p0); spans.Add(br.BitPosition);
                reg.Ints176.Add(br.ReadBS());
                if (dbg) System.Console.Error.WriteLine($"  VSDf[{fIdx++}] D={v:0.###}");
                return v;
            }
            CmcTriple Cmc()
            {
                curSlot = 200 + cmcSeq++;
                long p0 = br.BitPosition;
                var c = DwgObjectReader.ReadCmcTriple(br);
                if ((c.Rgb >> 24) is < 0xC0 or > 0xC8) throw new InvalidDataException($"CMC rgb=0x{c.Rgb:X8}");
                spans.Add(p0); spans.Add(br.BitPosition);
                reg.Ints176.Add(br.ReadBS());
                if (dbg) System.Console.Error.WriteLine($"  VSDf[{fIdx++}] CMC rgb=0x{c.Rgb:X8}");
                return new CmcTriple(c.Idx, c.Rgb, c.Flag);
            }
            S(0, () => br.ReadBL(), 27, hasInt: false);
            reg.ExtLightingModel = br.ReadBS();
            S(18, () => br.ReadB() ? 1 : 0, 1, hasInt: false);
            reg.FaceLightingModel = S(1, () => br.ReadBL(), 65535);
            reg.FaceLightingQuality = S(2, () => br.ReadBL(), 65535);
            reg.FaceColorMode = S(3, () => br.ReadBL(), 65535);
            reg.FaceModifier = S(4, () => br.ReadBS(), 255);
            reg.FaceOpacity = Span(() => br.ReadBD(), 0.0, 1.0);
            reg.FaceSpecular = Span(() => br.ReadBD(), 0.0, 1000.0);
            reg.FaceMonoColor = Cmc();
            reg.EdgeModel = S(5, () => br.ReadBL(), 65535);
            reg.EdgeStyle = S(6, () => br.ReadBL(), 65535);
            reg.EdgeIntersectionColor = Cmc();
            reg.EdgeObscuredColor = Cmc();
            reg.EdgeObscuredLinetype = S(7, () => br.ReadBL(), 65535);
            reg.EdgeIntersectionLinetype = S(8, () => br.ReadBL(), 255);
            reg.EdgeCreaseAngle = Span(() => br.ReadBD(), -360.0, 360.0);
            reg.EdgeModifier = S(9, () => br.ReadBL(), 65535);
            reg.EdgeColor = Cmc();
            reg.EdgeOpacity = Span(() => br.ReadBD(), 0.0, 1.0);
            reg.EdgeWidth = S(10, () => br.ReadBL(), 255);
            reg.EdgeOverhang = S(11, () => br.ReadBL(), 255);
            reg.EdgeJitter = S(12, () => br.ReadBL(), 255);
            reg.EdgeSilhouetteColor = Cmc();
            reg.EdgeSilhouetteWidth = S(13, () => br.ReadBL(), 255);
            S(14, () => br.ReadBL(), 255);
            reg.EdgeIsolines = S(15, () => br.ReadBL(), 255);
            reg.EdgeDoHidePrecision = S(19, () => br.ReadB() ? 1 : 0, 1) != 0;
            reg.DisplaySettings = S(16, () => br.ReadBL(), 65535);
            { double v = br.ReadBD();
              if (double.IsNaN(v)) throw new InvalidDataException();
              brightness = v; reg.Ints176.Add(br.ReadBS());
              if (dbg) System.Console.Error.WriteLine($"  VSDf[{fIdx++}] db={v:0.###}"); }
            reg.DisplayShadowType = S(17, () => br.ReadBL(), 65535);
        }
        catch (Exception ex)
        {
            Fail(br, $"字段区异常@slot{curSlot}:{ex.GetType().Name}({ex.Message})");
            if (dbg) System.Console.Error.WriteLine(
                $"VSD h={Rec.Handle:X} 字段区异常 body={bodyStart} slot={curSlot} @{br.BitPosition}: {ex.Message}");
            return false;
        }
        long fieldsEnd = br.BitPosition;
        long streamStart;
        if (SrcVersion >= DwgVersion.AC1027)
        {
            long anchor = Rec.StringStreamStartBit;
            if (anchor >= fieldsEnd && anchor < hs)
            {
                streamStart = anchor;
                br.SetPosition((int)(streamStart >> 3), (int)(streamStart & 7));
            }
            else
            {
                long pp = fieldsEnd + PropsBlockBits;
                br.SetPosition((int)(pp >> 3), (int)(pp & 7));
                streamStart = br.BitPosition;
            }
        }
        else
        {
            streamStart = fieldsEnd;
        }
        try
        {
            var savedStream = br.StringStream;
            br.StringStream = null;
            br.UnicodeStrings = true;
            string desc = br.ReadTV();
            if (SrcVersion >= DwgVersion.AC1027) br.ReadTV();
            br.UnicodeStrings = false;
            br.StringStream = savedStream;
            int ssSize = br.ReadRS();
            bool hasStrings = br.ReadB();
            if (!hasStrings || br.BitPosition != streamStart + ssSize + 17
                || br.BitPosition != hs)
            {
                Fail(br, !hasStrings ? "页脚 has_strings=0"
                    : br.BitPosition != streamStart + ssSize + 17 ? "页脚 ssSize 不符"
                    : "页脚末位不落句柄流起点");
                if (dbg) System.Console.Error.WriteLine(
                    $"VSD h={Rec.Handle:X} 页脚不闭合: body={bodyStart} ss={streamStart} " +
                    $"descLen={desc.Length} ssSize={ssSize} has={hasStrings} end={br.BitPosition} hs={hs}");
                return false;
            }
            if (desc.Length == 0 || desc.Length > 255)
            { Fail(br, $"串流 desc 长度={desc.Length}"); return false; }
            reg.CommitTo(Cap, Family);
            if (!bdRangeOk)
            { Fail(br, $"BD 值域越手术档@{bdTag}(值轴已提交)"); return false; }
            Cap.DownCaptured = true;
            Cap.DownBodyStartBit = bodyStart;
            Cap.DownStreamStartBit = streamStart;
            Cap.DownFieldsEndBit = fieldsEnd;
            Cap.DownFieldSpans = spans.ToArray();
            Cap.DownScalars = scalars;
            Cap.DownHaloGap = scalars[14];
            Cap.DownBrightness = brightness;
            StyleType = scalars[0];
            Description = desc;
            EdgeHaloGap = scalars[14];
            DisplayBrightness = brightness;
            InternalOnly = scalars[18] != 0;
            Captured = true;
            Rec.VisualStyleModel = this;
            if (dbg) System.Console.Error.WriteLine(
                $"VSD h={Rec.Handle:X} OK body={bodyStart} ss={streamStart} hs={hs}");
            return true;
        }
        catch (Exception ex)
        { br.UnicodeStrings = false; Fail(br, $"串流区异常:{ex.GetType().Name}"); return false; }
    }
}
