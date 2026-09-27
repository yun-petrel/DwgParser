using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class ViewStyleAcR2007Plus : ViewStyleRecord
{
    protected ViewStyleAcR2007Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected abstract int SpecStringCount { get; }

    protected abstract ViewStyleValues NewValues();

    protected abstract bool ReadCentralTail(BitReader br, ViewStyleValues v);

    internal override void Read(BitReader br)
    {
        if (Rec.SourceVersion < DwgVersion.AC1021) { Fail(br, "非 R2007+ 源"); return; }
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        if (!CentralStrings.TryDataBits(Rec, ObjStart, br, out uint bitsize, out _))
        { Fail(br, "集中流 bitsize 未定位"); return; }
        if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bitsize,
                out BitReader str, out long areaStart, out long areaEnd))
        { Fail(br, "集中流串区量不出"); return; }
        if (!CentralStrings.ReadAll(str, areaEnd, 16, out var tus))
        { Fail(br, "集中流 TU 逐枚读出越界"); return; }
        if (tus.Count < SpecStringCount)
        { Fail(br, $"集中流 TU 枚数={tus.Count}<{SpecStringCount}(spec T 字段数)"); return; }

        var v = NewValues();
        try
        {
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return; }
            int numReactors = br.ReadBL();
            if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return; }
            br.ReadB();
            if (Rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            v.MdocClassVersion = br.ReadBS();
            if (v.MdocClassVersion != 0) { Fail(br, $"mdoc_class_version={v.MdocClassVersion}"); return; }
            v.IsModifiedForRecompute = br.ReadB();
            if (Rec.SourceVersion >= DwgVersion.AC1032)
                v.ViewstyleFlags = br.ReadBL();
            v.ClassVersion = br.ReadBS();
            if (v.ClassVersion != 0) { Fail(br, $"class_version={v.ClassVersion}"); return; }

            v.Flags = unchecked((uint)br.ReadBL());
            v.IdentifierColor = ReadColor(br);
            v.IdentifierHeight = Bd(br);
        }
        catch (Exception ex)
        { Fail(br, $"字段区异常:{ex.GetType().Name}"); return; }
        if (!ReadCentralTail(br, v)) return;
        long tailEnd = br.BitPosition;
        if (tailEnd != areaStart)
        { Fail(br, $"数值区末不落串区起点(Δ={tailEnd - areaStart})"); return; }

        int need = SpecStringCount + (v.ViewstyleFlags != ViewStyleValues.NoField ? 1 : 0);
        if (tus.Count < need)
        { Fail(br, $"集中流 TU 枚数={tus.Count}<{need}(vf={(v.ViewstyleFlags != ViewStyleValues.NoField ? "在" : "无")})"); return; }
        int i = 0;
        v.Description = tus[i++];
        if (v.ViewstyleFlags != ViewStyleValues.NoField) v.DisplayName = tus[i++];
        v.IdentifierExcludeCharacters = tus[i++];
        v.ViewlabelPattern = tus[i++];
        if (v is SectionViewStyleValues sec) sec.HatchPattern = tus[i++];
        v.Layout = Family;
        Cap.FieldValues = v;
        Cap.ValuesCommitted = true;
        for (; i < tus.Count; i++) Cap.ExtraStrings.Add(tus[i]);
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"VSR2007 h={Rec.Handle:X} t={Rec.TypeCode} {Family} {Rec.SourceVersion} " +
                $"tailEnd={tailEnd} area=[{areaStart}..{areaEnd}] tus={tus.Count}(need {need}) " +
                $"flags=0x{v.Flags:X} idh={v.IdentifierHeight} idc={v.IdentifierColor.Index}/0x{v.IdentifierColor.Rgb:X6} " +
                $"vf={v.ViewstyleFlags} nas={(v is SectionViewStyleValues sv ? sv.NumHatchAngles : -1)} " +
                $"desc='{v.Description}' dn='{v.DisplayName}' ex='{v.IdentifierExcludeCharacters}' " +
                $"pat='{v.ViewlabelPattern}' hat='{(v is SectionViewStyleValues sv2 ? sv2.HatchPattern : "")}' " +
                $"extra={Cap.ExtraStrings.Count}");
    }


    protected static double Bd(BitReader br)
    {
        double v = br.ReadBD();
        if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12)
            throw new InvalidDataException($"BD={v}");
        return v;
    }

    protected static ViewStyleValues.Color ReadColor(BitReader br)
    {
        short idx = DwgObjectReader.ReadCmc(br, out uint rgb, out string name, out string book);
        byte flag = (byte)((name.Length > 0 ? 1 : 0) | (book.Length > 0 ? 2 : 0));
        return new ViewStyleValues.Color(idx, rgb, flag, name, book);
    }
}

internal sealed class SectionViewStyleAcR2007Plus : ViewStyleAcR2007Plus
{
    public SectionViewStyleAcR2007Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "SECTIONVIEWSTYLE(R2007+)";

    protected override int SpecStringCount => 4;

    protected override ViewStyleValues NewValues() => new SectionViewStyleValues();

    protected override bool ReadCentralTail(BitReader br, ViewStyleValues v)
    {
        var s = (SectionViewStyleValues)v;
        try
        {
            s.ArrowSymbolColor = ReadColor(br);
            s.ArrowSymbolSize = Bd(br);
            s.ArrowSymbolExtensionLength = Bd(br);
            s.PlaneLinewt = br.ReadBL();
            s.PlaneLineColor = ReadColor(br);
            s.BendLinewt = br.ReadBL();
            s.BendLineColor = ReadColor(br);
            s.BendLineLength = Bd(br);
            s.EndLineLength = Bd(br);
            v.ViewlabelTextColor = ReadColor(br);
            v.ViewlabelTextHeight = Bd(br);
            v.ViewlabelAttachment = br.ReadBL();
            v.ViewlabelOffset = Bd(br);
            v.ViewlabelAlignment = br.ReadBL();
            s.HatchColor = ReadColor(br);
            s.HatchBgColor = ReadColor(br);
            s.HatchScale = Bd(br);
            s.HatchTransparency = br.ReadBL();
            s.UnknownB1 = br.ReadB();
            s.UnknownB2 = br.ReadB();
            s.IdentifierPosition = br.ReadBL();
            v.IdentifierOffset = Bd(br);
            s.ArrowPosition = br.ReadBL();
            s.EndLineOvershoot = Bd(br);
            int n = br.ReadBL();
            if (n is < 0 or > 1024)
            { Fail(br, $"num_hatch_angles={n}"); return false; }
            s.NumHatchAngles = n;
            for (int k = 0; k < n; k++) s.HatchAngles.Add(Bd(br));
        }
        catch (Exception ex)
        { Fail(br, $"510 尾段异常:{ex.GetType().Name}"); return false; }
        return true;
    }
}

internal sealed class DetailViewStyleAcR2007Plus : ViewStyleAcR2007Plus
{
    public DetailViewStyleAcR2007Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "DETAILVIEWSTYLE(R2007+)";

    protected override int SpecStringCount => 3;

    protected override ViewStyleValues NewValues() => new DetailViewStyleValues();

    protected override bool ReadCentralTail(BitReader br, ViewStyleValues v)
    {
        var d = (DetailViewStyleValues)v;
        try
        {
            d.IdentifierOffset = Bd(br);
            d.IdentifierPlacement = br.ReadRC();
            d.ArrowSymbolColor = ReadColor(br);
            d.ArrowSymbolSize = Bd(br);
            d.BoundaryLinewt = br.ReadBL();
            d.BoundaryLineColor = ReadColor(br);
            d.ViewlabelTextColor = ReadColor(br);
            d.ViewlabelTextHeight = Bd(br);
            d.ViewlabelAttachment = br.ReadBL();
            d.ViewlabelOffset = Bd(br);
            d.ViewlabelAlignment = br.ReadBL();
            d.ConnectionLinewt = br.ReadBL();
            d.ConnectionLineColor = ReadColor(br);
            d.BorderlineLinewt = br.ReadBL();
            d.BorderlineColor = ReadColor(br);
            d.ModelEdge = br.ReadRC();
        }
        catch (Exception ex)
        { Fail(br, $"511 尾段异常:{ex.GetType().Name}"); return false; }
        return true;
    }
}
