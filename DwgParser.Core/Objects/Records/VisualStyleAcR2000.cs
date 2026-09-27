using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleAcR2000 : VisualStyleRecord
{
    public override string Family => "R2000";

    public VisualStyleAcR2000(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion != DwgVersion.AC1015) return false;
        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return false; }
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
        long eedLen = br.BitPosition - eedStart;
        int numReactors = br.ReadBL();
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
        var cmc = new int[5];
        int cmcSlot = 0;
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
                if (dbg) System.Console.Error.WriteLine($"  VSf0[{my}] {nm} {br.BitPosition - p0}b");
                return v;
            }
            double NumD(Func<double> rd)
            {
                int my = fIdx++;
                long p0 = br.BitPosition; double v = rd(); spans.Add(p0); spans.Add(br.BitPosition);
                if (dbg) System.Console.Error.WriteLine($"  VSf0[{my}] D {br.BitPosition - p0}b");
                return v;
            }
            CmcTriple NumCmc()
            {
                int my = fIdx++;
                long p0 = br.BitPosition;
                int idx = br.ReadBS();
                if (idx is < 0 or > 257) throw new InvalidDataException();
                cmc[cmcSlot++] = idx;
                spans.Add(p0); spans.Add(br.BitPosition);
                if (dbg) System.Console.Error.WriteLine($"  VSf0[{my}] CMC-BSo idx={idx} {br.BitPosition - p0}b");
                return new CmcTriple(idx, 0, 0);
            }
            { long p0 = br.BitPosition; int st = br.ReadBL(); spans.Add(p0); spans.Add(br.BitPosition);
              if (st is < 0 or > 27) { Fail(br, $"style_type={st}"); return false; } styleType = st; }
            reg.FaceLightingModel = Num(() => br.ReadBL());
            reg.FaceLightingQuality = Num(() => br.ReadBL());
            reg.FaceColorMode = Num(() => br.ReadBL());
            reg.FaceOpacity = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.FaceSpecular = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.FaceMonoColor = NumCmc();
            reg.FaceModifier = Num(() => br.ReadBL());
            reg.EdgeModel = Num(() => br.ReadBL());
            reg.EdgeStyle = Num(() => br.ReadBL());
            reg.EdgeIntersectionColor = NumCmc();
            reg.EdgeObscuredColor = NumCmc();
            reg.EdgeObscuredLinetype = Num(() => br.ReadBL());
            reg.EdgeCreaseAngle = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.EdgeModifier = Num(() => br.ReadBL());
            reg.EdgeColor = NumCmc();
            reg.EdgeOpacity = NumD(() => { double v = br.ReadBD(); if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) throw new InvalidDataException($"v={v:R}"); return v; });
            reg.EdgeWidth = Num(() => br.ReadBS());
            reg.EdgeOverhang = Num(() => br.ReadBS());
            reg.EdgeJitter = Num(() => br.ReadBL());
            reg.EdgeSilhouetteColor = NumCmc();
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
                $"VS0RC h={Rec.Handle:X} 字段区异常@{br.BitPosition}: {ex.Message}");
            return false;
        }
        if (cmcSlot != 5) { Fail(br, $"CMC 槽={cmcSlot}≠5"); return false; }
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
                $"VS0RC h={Rec.Handle:X} 不闭合: desc={descStart} body={spans[0]} io@{br.BitPosition} hs={hs}");
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
        Cap.PreR2004CmcIndices = cmc;
        Cap.HsBit = hs;
        Cap.EedStartBit = eedStart;
        Cap.EedBitLen = (int)eedLen;
        StyleType = styleType;
        Description = desc;
        EdgeHaloGap = haloGap;
        DisplayBrightness = brightness;
        InternalOnly = internalOnly != 0;
        Captured = true;
        Rec.VisualStyleModel = this;
        if (dbg) System.Console.Error.WriteLine(
            $"VS0RC h={Rec.Handle:X} OK desc={descStart}..{spans[0]} hs={hs} spans={spans.Count} " +
            $"cmc=[{string.Join(",", cmc)}]");
        return true;
    }
}
