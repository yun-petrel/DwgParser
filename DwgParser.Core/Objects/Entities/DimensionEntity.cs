using System.Collections.Generic;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public abstract class DimensionEntity : DwgEntity, IDimension
{
    protected abstract string SubtypeName { get; }

    protected abstract void ReadSubtypeGeometry(BitReader br);

    public void ReadGeometry(BitReader br, DwgObjectRecord? record, List<DwgObjectReader.DimFieldEntry>? trace,
        DwgVersion version)
    {
        if (version.IsR2010Plus())
        {
            DimensionClassVersion = br.ReadRC();
            if (trace != null)
                trace.Add(new DwgObjectReader.DimFieldEntry("class_version RC", br.BitPosition - 8, -8,
                    DimensionClassVersion.ToString()));
        }

        long baseBit = br.BitPosition;
        void Cap(string name, object? v)
        {
            if (trace != null) trace.Add(new DwgObjectReader.DimFieldEntry(name, br.BitPosition, br.BitPosition - baseBit, v?.ToString() ?? "null"));
        }

        var extrusion = br.Read3BD(); Cap("extrusion 3BD", extrusion);
        var textMid = br.Read2RD(); Cap("text_midpt 2RD", textMid);
        var elevation = br.ReadBD(); Cap("elevation BD", elevation);
        byte flag1 = br.ReadRC(); Cap("flag1 RC", flag1);
        DimensionTextFlags = flag1;
        long utStart = br.BitPosition;
        string userText = br.ReadTV();
        record?.CaptureString(utStart, br.BitPosition, userText);
        if (record != null) { record.DimUserTextSlotBit = utStart; record.DimUserText = userText; }
        Cap("user_text TV", $"len={userText.Length} \"{userText}\"");
        double textRotation = br.ReadBD(); Cap("text_rotation BD", textRotation);
        double horizDir = br.ReadBD(); Cap("horiz_dir BD", horizDir);
        TextHorizontalDir = horizDir;
        var insScale = br.Read3BD(); Cap("ins_scale 3BD", insScale);
        double insRotation = br.ReadBD(); Cap("ins_rotation BD", insRotation);
        short attachment = br.ReadBS(); Cap("attachment BS", attachment);
        TextAttachment = attachment;
        long lspaceStyle = br.ReadBS(); Cap("lspace_style BS", lspaceStyle);
        double lspaceFactor = br.ReadBD(); Cap("lspace_factor BD", lspaceFactor);
        double actMeasurement = br.ReadBD(); Cap("act_measurement BD", actMeasurement);
        if (record != null) record.DimActMeasurementEndBit = br.BitPosition;
        if (version >= DwgVersion.AC1021)
        {
            bool dimUnknown = br.ReadB(); Cap("unknown B", dimUnknown);
            bool flipArrow1 = br.ReadB(); Cap("flip_arrow1 B", flipArrow1);
            bool flipArrow2 = br.ReadB(); Cap("flip_arrow2 B", flipArrow2);
            DimFlipArrow1 = flipArrow1;
            DimFlipArrow2 = flipArrow2;
        }
        var cloneInsPt = br.Read2RD(); Cap("clone_ins_pt 2RD", cloneInsPt);
        DimensionCloneInsPt = new Vec2(cloneInsPt.X, cloneInsPt.Y);
        Cap("<<公共段读完 / 首个子类型点起点>>", br.BitPosition);

        Kind = DwgEntityType.Dimension;
        TextValue = userText;
        Height = 2.5;
        Rotation = insRotation;
        Elevation = elevation;
        ExtrusionZ = extrusion.Z;
        DimLspaceStyle = lspaceStyle;
        DimLspaceFactor = lspaceFactor;
        DimInsScale = insScale;
        Start = new Vec2(textMid.X, textMid.Y);
        Measurement = actMeasurement;
        DimTextMid = new Vec2(textMid.X, textMid.Y);
        DimTextRotation = textRotation;
        Points.Clear();
        DimSubtype = SubtypeName;

        ReadSubtypeGeometry(br);

        if (Points.Count >= 2 && Start.X == 0 && Start.Y == 0)
            Start = new Vec2((Points[0].X + Points[1].X) / 2, (Points[0].Y + Points[1].Y) / 2);
    }

    protected void AddPt((double X, double Y, double Z) p)
    {
        if (double.IsFinite(p.X) && double.IsFinite(p.Y))
            Points.Add(new Vec2(p.X, p.Y));
    }


    public double LeaderLength { get; private set; }

    public int DimensionClassVersion { get; private set; } = -1;

    public int OrdinateAxisFlag { get; private set; }

    public bool DimFlipArrow1 { get; private set; }

    public bool DimFlipArrow2 { get; private set; }

    public long DimLspaceStyle { get; private set; }

    public double DimLspaceFactor { get; private set; }

    public (double X, double Y, double Z) DimInsScale { get; private set; }


    public double TextHorizontalDir { get; private set; }

    public byte DimensionTextFlags { get; private set; }

    public Vec2 DimensionCloneInsPt { get; private set; }

    protected void SetLeaderLength(double v) => LeaderLength = v;
    protected void SetOrdinateAxisFlag(int v) => OrdinateAxisFlag = v;
}
