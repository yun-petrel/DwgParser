using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleAcR2004 : VisualStyleRecord
{
    public override string Family => "R2004";

    public VisualStyleAcR2004(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion != DwgVersion.AC1018) return false;
        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return false; }
        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        br.ReadHandle();
        Rec.EedStartBit = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
        Rec.EedBitLen = (int)(br.BitPosition - Rec.EedStartBit);
        int numReactors = br.ReadBL();
        br.ReadB();
        if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return false; }
        long descStart = br.BitPosition;
        br.UnicodeStrings = false;
        string desc;
        try { desc = br.ReadTV(); }
        catch { br.UnicodeStrings = false; Fail(br, "内联串 desc 越界"); return false; }
        br.UnicodeStrings = false;
        if (string.IsNullOrEmpty(desc) || desc.Length > 255)
        { Fail(br, $"内联 desc 长度={desc.Length}"); return false; }
        var spans = new List<long>(60);
        var reg = new VisualStyleRegion();
        int haloGap = 0;
        double brightness = 0.0;
        int styleType = 0;
        int fIdx = 0;
        try
        {
            int Num(Func<int> rd, string nm = "")
            {
                int my = fIdx++;
                long p0 = br.BitPosition; int v = rd(); spans.Add(p0); spans.Add(br.BitPosition);
                if (dbg) System.Console.Error.WriteLine($"  VSf4[{my}] {nm} {br.BitPosition - p0}b");
                return v;
            }
            double NumD(Func<double> rd)
            {
                int my = fIdx++;
                long p0 = br.BitPosition; double v = rd(); spans.Add(p0); spans.Add(br.BitPosition);
                if (dbg) System.Console.Error.WriteLine($"  VSf4[{my}] D {br.BitPosition - p0}b");
                return v;
            }
            CmcTriple NumC(Func<(int, uint, byte)> rd)
            {
                int my = fIdx++;
                long p0 = br.BitPosition;
                var c = rd();
                if ((c.Item2 >> 24) is < 0xC0 or > 0xC8) throw new InvalidDataException();
                spans.Add(p0); spans.Add(br.BitPosition);
                if (dbg) System.Console.Error.WriteLine($"  VSf4[{my}] CMC idx={c.Item1} rgb=0x{c.Item2:X8} {br.BitPosition - p0}b");
                return new CmcTriple(c.Item1, c.Item2, c.Item3);
            }
            { long p0 = br.BitPosition; int st = br.ReadBL(); spans.Add(p0); spans.Add(br.BitPosition);
              if (st is < 0 or > 27) { Fail(br, $"style_type={st}"); return false; } styleType = st; }
            reg.FaceLightingModel = Num(() => br.ReadBL());
            reg.FaceLightingQuality = Num(() => br.ReadBL());
            reg.FaceColorMode = Num(() => br.ReadBL());
            reg.FaceOpacity = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.FaceSpecular = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.FaceMonoColor = NumC(() => DwgObjectReader.ReadCmcTriple(br));
            reg.FaceModifier = Num(() => br.ReadBL());
            reg.EdgeModel = Num(() => br.ReadBL());
            reg.EdgeStyle = Num(() => br.ReadBL());
            reg.EdgeIntersectionColor = NumC(() => DwgObjectReader.ReadCmcTriple(br));
            reg.EdgeObscuredColor = NumC(() => DwgObjectReader.ReadCmcTriple(br));
            reg.EdgeObscuredLinetype = Num(() => br.ReadBL());
            reg.EdgeCreaseAngle = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.EdgeModifier = Num(() => br.ReadBL());
            reg.EdgeColor = NumC(() => DwgObjectReader.ReadCmcTriple(br));
            reg.EdgeOpacity = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.EdgeWidth = Num(() => br.ReadBS());
            reg.EdgeOverhang = Num(() => br.ReadBS());
            reg.EdgeJitter = Num(() => br.ReadBL());
            reg.EdgeSilhouetteColor = NumC(() => DwgObjectReader.ReadCmcTriple(br));
            reg.EdgeSilhouetteWidth = Num(() => br.ReadBS());
            { long p0 = br.BitPosition; haloGap = br.ReadRC(); spans.Add(p0); spans.Add(br.BitPosition); }
            reg.EdgeIsolines = Num(() => br.ReadBS());
            reg.EdgeDoHidePrecision = Num(() => br.ReadB() ? 1 : 0) != 0;
            reg.EdgeStyleApply = Num(() => br.ReadBS());
            reg.HasEdgeStyleApply = true;
            reg.EdgeIntersectionLinetype = Num(() => br.ReadBS());
            reg.DisplaySettings = Num(() => br.ReadBL());
            { long p0 = br.BitPosition; brightness = br.ReadBL(); spans.Add(p0); spans.Add(br.BitPosition); }
            reg.DisplayShadowType = Num(() => br.ReadBL());
        }
        catch (Exception ex)
        {
            Fail(br, $"字段区异常@f{fIdx}:{ex.GetType().Name}({ex.Message})");
            if (dbg) System.Console.Error.WriteLine(
                $"VS4RC h={Rec.Handle:X} 字段区异常@{br.BitPosition}: {ex.Message}");
            return false;
        }
        byte internalOnly = 0;
        try
        {
            internalOnly = (byte)(br.ReadB() ? 1 : 0);
        }
        catch { Fail(br, "internal_only 位读不出"); return false; }
        if (br.BitPosition != hs)
        {
            Fail(br, br.BitPosition > hs ? "不闭合(游标越过帧尾)" : "不闭合(游标未达帧尾)");
            if (dbg) System.Console.Error.WriteLine(
                $"VS4RC h={Rec.Handle:X} 不闭合: desc={descStart} body={spans[0]} io@{br.BitPosition} hs={hs}");
            return false;
        }
        reg.CommitTo(Cap, Family);
        Cap.Captured = true;
        Cap.BodyStartBit = descStart;
        Cap.StreamStartBit = spans[0];
        Cap.FieldSpans = spans.ToArray();
        Cap.HaloGap = haloGap;
        Cap.Brightness = brightness;
        Cap.InternalOnly = internalOnly;
        StyleType = styleType;
        Description = desc;
        EdgeHaloGap = haloGap;
        DisplayBrightness = brightness;
        InternalOnly = internalOnly != 0;
        Captured = true;
        Rec.VisualStyleModel = this;
        if (dbg) System.Console.Error.WriteLine(
            $"VS4RC h={Rec.Handle:X} OK desc={descStart}..{spans[0]} hs={hs} spans={spans.Count}");
        return true;
    }
}
