using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class ViewStyleAcR2000 : ViewStyleRecord
{
    protected ViewStyleAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected abstract ViewStyleValues NewValues();

    protected abstract bool ReadR2000Tail(BitReader br, ViewStyleValues v);

    internal override void Read(BitReader br)
    {
        if (Rec.SourceVersion != DwgVersion.AC1015) { Fail(br, "非 AC1015 源"); return; }
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (bitsize == 0 || hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8)
        { Fail(br, $"帧界(RL vs BodyEnd)(rl={bitsize})"); return; }
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return; }
        int numReactors = br.ReadBL();
        if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return; }
        var v = NewValues();
        try
        {
            v.MdocClassVersion = br.ReadBS();
            if (v.MdocClassVersion != 0) { Fail(br, $"mdoc_class_version={v.MdocClassVersion}"); return; }
            v.Description = Tv(br);
            v.IsModifiedForRecompute = br.ReadB();
            v.ClassVersion = br.ReadBS();
            if (v.ClassVersion != 0) { Fail(br, $"class_version={v.ClassVersion}"); return; }
            v.Flags = unchecked((uint)br.ReadBL());
            v.IdentifierColor = ShortColor(br);
            v.IdentifierHeight = Bd(br);
        }
        catch (Exception ex)
        { Fail(br, $"字段区异常:{ex.GetType().Name}"); return; }
        if (!ReadR2000Tail(br, v)) return;
        long end = br.BitPosition;
        if (end != hs) { Fail(br, $"不闭合(游标未落 RL 锚)(Δ={end - hs})"); return; }

        v.Layout = Family;
        Cap.FieldValues = v;
        Cap.ValuesCommitted = true;
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"VSR2000 h={Rec.Handle:X} t={Rec.TypeCode} {Family} hs={hs} end={end} " +
                $"mod={v.IsModifiedForRecompute} flags=0x{v.Flags:X} idh={v.IdentifierHeight} " +
                $"idc={v.IdentifierColor.Index} desc='{v.Description}' " +
                $"ex='{v.IdentifierExcludeCharacters}' pat='{v.ViewlabelPattern}' " +
                $"hat='{(v is SectionViewStyleValues s ? s.HatchPattern : "")}' " +
                $"nas={(v is SectionViewStyleValues sv ? sv.NumHatchAngles : -1)}");
    }


    protected static double Bd(BitReader br)
    {
        double v = br.ReadBD();
        if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12)
            throw new InvalidDataException($"BD={v}");
        return v;
    }

    protected static ViewStyleValues.Color ShortColor(BitReader br)
    {
        short idx = br.ReadBS();
        if (idx < 0 || idx > 257) throw new InvalidDataException($"CMC idx({idx})");
        return new ViewStyleValues.Color(idx, 0u, 0, "", "");
    }

    protected static string Tv(BitReader br)
    {
        string s = br.ReadTV();
        if (s.Length > 255) throw new InvalidDataException($"TV 长 {s.Length}");
        return s;
    }
}

internal sealed class SectionViewStyleAcR2000 : ViewStyleAcR2000
{
    public SectionViewStyleAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "SECTIONVIEWSTYLE(R2000)";

    protected override ViewStyleValues NewValues() => new SectionViewStyleValues();

    protected override bool ReadR2000Tail(BitReader br, ViewStyleValues v)
    {
        var s = (SectionViewStyleValues)v;
        string at = "";
        try
        {
            at = "arrow_symbol_color"; s.ArrowSymbolColor = ShortColor(br);
            at = "arrow_symbol_size"; s.ArrowSymbolSize = Bd(br);
            at = "exclude"; s.IdentifierExcludeCharacters = Tv(br);
            at = "arrow_symbol_extension_length"; s.ArrowSymbolExtensionLength = Bd(br);
            at = "plane_linewt"; s.PlaneLinewt = br.ReadBL();
            at = "plane_line_color"; s.PlaneLineColor = ShortColor(br);
            at = "bend_linewt"; s.BendLinewt = br.ReadBL();
            at = "bend_line_color"; s.BendLineColor = ShortColor(br);
            at = "bend_line_length"; s.BendLineLength = Bd(br);
            at = "end_line_length"; s.EndLineLength = Bd(br);
            at = "viewlabel_text_color"; v.ViewlabelTextColor = ShortColor(br);
            at = "viewlabel_text_height"; v.ViewlabelTextHeight = Bd(br);
            at = "viewlabel_attachment"; v.ViewlabelAttachment = br.ReadBL();
            at = "viewlabel_offset"; v.ViewlabelOffset = Bd(br);
            at = "viewlabel_alignment"; v.ViewlabelAlignment = br.ReadBL();
            at = "viewlabel_pattern"; v.ViewlabelPattern = Tv(br);
            at = "hatch_color"; s.HatchColor = ShortColor(br);
            at = "hatch_bg_color"; s.HatchBgColor = ShortColor(br);
            at = "hatch_pattern"; s.HatchPattern = Tv(br);
            at = "hatch_scale"; s.HatchScale = Bd(br);
            at = "hatch_transparency"; s.HatchTransparency = br.ReadBL();
            at = "unknown_b1"; s.UnknownB1 = br.ReadB();
            at = "unknown_b2"; s.UnknownB2 = br.ReadB();
            at = "identifier_position"; s.IdentifierPosition = br.ReadBL();
            at = "identifier_offset"; v.IdentifierOffset = Bd(br);
            at = "arrow_position"; s.ArrowPosition = br.ReadBL();
            at = "end_line_overshoot"; s.EndLineOvershoot = Bd(br);
            at = "num_hatch_angles";
            int n = br.ReadBL();
            if (n is < 0 or > 1024)
            { Fail(br, $"510 num_hatch_angles={n}"); return false; }
            s.NumHatchAngles = n;
            at = "hatch_angles";
            for (int k = 0; k < n; k++) s.HatchAngles.Add(Bd(br));
        }
        catch (Exception ex)
        { Fail(br, $"510 尾段@{at}:{ex.GetType().Name}"); return false; }
        return true;
    }
}

internal sealed class DetailViewStyleAcR2000 : ViewStyleAcR2000
{
    public DetailViewStyleAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "DETAILVIEWSTYLE(R2000)";

    protected override ViewStyleValues NewValues() => new DetailViewStyleValues();

    protected override bool ReadR2000Tail(BitReader br, ViewStyleValues v)
    {
        var d = (DetailViewStyleValues)v;
        try
        {
            v.IdentifierExcludeCharacters = Tv(br);
            v.IdentifierOffset = Bd(br);
            d.IdentifierPlacement = br.ReadRC();
            d.ArrowSymbolColor = ShortColor(br);
            d.ArrowSymbolSize = Bd(br);
            d.BoundaryLinewt = br.ReadBL();
            d.BoundaryLineColor = ShortColor(br);
            v.ViewlabelTextColor = ShortColor(br);
            v.ViewlabelTextHeight = Bd(br);
            v.ViewlabelAttachment = br.ReadBL();
            v.ViewlabelOffset = Bd(br);
            v.ViewlabelAlignment = br.ReadBL();
            v.ViewlabelPattern = Tv(br);
            d.ConnectionLinewt = br.ReadBL();
            d.ConnectionLineColor = ShortColor(br);
            d.BorderlineLinewt = br.ReadBL();
            d.BorderlineColor = ShortColor(br);
            d.ModelEdge = br.ReadRC();
        }
        catch (Exception ex)
        { Fail(br, $"511 尾段异常:{ex.GetType().Name}"); return false; }
        return true;
    }
}
