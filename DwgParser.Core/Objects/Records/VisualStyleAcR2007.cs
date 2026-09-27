using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleAcR2007 : VisualStyleRecord
{
    public override string Family => "R2007";

    public VisualStyleAcR2007(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion != DwgVersion.AC1021) return false;
        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return false; }
        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
        Cap.EedStartBit = eedStart;
        Cap.EedBitLen = (int)(br.BitPosition - eedStart);
        if (Cap.EedBitLen < 2) { Fail(br, $"EED 长度异常={Cap.EedBitLen}位"); return false; }
        int numReactors = br.ReadBL();
        br.ReadB();
        if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return false; }
        long bodyStart = br.BitPosition;
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
                if (dbg) System.Console.Error.WriteLine($"  VSf[{my}] {nm} {br.BitPosition - p0}b");
                return v;
            }
            double NumD(Func<double> rd)
            {
                int my = fIdx++;
                long p0 = br.BitPosition; double v = rd(); spans.Add(p0); spans.Add(br.BitPosition);
                if (dbg) System.Console.Error.WriteLine($"  VSf[{my}] D {br.BitPosition - p0}b");
                return v;
            }
            CmcTriple NumC(Func<(int, uint, byte)> rd)
            {
                int my = fIdx++;
                long p0 = br.BitPosition;
                var c = rd();
                if ((c.Item2 >> 24) is < 0xC0 or > 0xC8) throw new InvalidDataException();
                spans.Add(p0); spans.Add(br.BitPosition);
                if (dbg) System.Console.Error.WriteLine($"  VSf[{my}] CMC idx={c.Item1} rgb=0x{c.Item2:X8} {br.BitPosition - p0}b");
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
            reg.Bd2007Field45 = NumD(() => { double v = br.ReadBD(); if (v is < 0.0 or > 10.0) throw new InvalidDataException(); return v; });
            spans.RemoveRange(spans.Count - 2, 2);
        }
        catch (Exception ex)
        {
            Fail(br, $"字段区异常@f{fIdx}:{ex.GetType().Name}({ex.Message})");
            if (dbg) System.Console.Error.WriteLine(
                $"VSRC h={Rec.Handle:X} 字段区异常@{br.BitPosition}: {ex.Message}");
            return false;
        }
        byte internalOnly = 0;
        try
        {
            internalOnly = (byte)(br.ReadB() ? 1 : 0);
        }
        catch { Fail(br, "internal_only 位读不出"); return false; }
        long streamStart = br.BitPosition;
        try
        {
            br.UnicodeStrings = true;
            string desc = br.ReadTV();
            br.UnicodeStrings = false;
            int ssSize = br.ReadRS();
            bool hasStrings = br.ReadB();
            if (!hasStrings || br.BitPosition != streamStart + ssSize + 17
                || br.BitPosition != hs)
            {
                Fail(br, !hasStrings ? "页脚 has_strings=0"
                    : br.BitPosition != streamStart + ssSize + 17 ? "页脚 ssSize 不符"
                    : "页脚末位不落句柄流起点");
                if (dbg) System.Console.Error.WriteLine(
                    $"VSRC h={Rec.Handle:X} 页脚不闭合: body={bodyStart} ss={streamStart} " +
                    $"descLen={desc.Length} ssSize={ssSize} has={hasStrings} end={br.BitPosition} hs={hs}");
                return false;
            }
            if (desc.Length == 0 || desc.Length > 255)
            { Fail(br, $"串流 desc 长度={desc.Length}"); return false; }
            reg.CommitTo(Cap, Family);
            Cap.Captured = true;
            Cap.BodyStartBit = bodyStart;
            Cap.StreamStartBit = streamStart;
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
                $"VSRC h={Rec.Handle:X} OK body={bodyStart} ss={streamStart} hs={hs} spans={spans.Count}");
            return true;
        }
        catch (Exception ex)
        { br.UnicodeStrings = false; Fail(br, $"串流区异常:{ex.GetType().Name}"); return false; }
    }
}
