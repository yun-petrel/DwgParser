using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class ViewportAcR2000Plus : ViewportEntity
{
    internal bool HasGridMajor { get; set; }

    internal bool HasShadePlot { get; set; }

    internal bool HasLightingBlock { get; set; }

    public override void ReadBody(BitReader br)
    {
        ReadHead(br);

        var target = br.Read3BD();
        VportViewTarget = new Vec2(target.X, target.Y);
        var dir = br.Read3BD();
        VportViewDirection = new Vec2(dir.X, dir.Y);
        VportTwist = br.ReadBD();
        VportViewSize = br.ReadBD();
        LensLength = br.ReadBD();
        VportFrontClipZ = br.ReadBD();
        VportBackClipZ = br.ReadBD();
        VportSnapAngle = br.ReadBD();
        var vc = br.Read2RD();
        VportViewCenter = new Vec2(vc.X, vc.Y);
        var sb = br.Read2RD();
        SnapBase = new Vec2(sb.X, sb.Y);
        var su = br.Read2RD();
        SnapUnit = new Vec2(su.X, su.Y);
        var gu = br.Read2RD();
        GridUnit = new Vec2(gu.X, gu.Y);
        CircleZoom = br.ReadBS();
        if (HasGridMajor) GridMajor = br.ReadBS();
        NumFrozenLayers = br.ReadBL();
        StatusFlag = br.ReadBL();
        StyleSheet = br.ReadTV();
        RenderMode = br.ReadRC();

        UcsAtOrigin = br.ReadB();
        UcsVp = br.ReadB();
        var uo = br.Read3BD();
        UcsOrigin = new Vec3(uo.X, uo.Y, uo.Z);
        var ux = br.Read3BD();
        UcsXDir = new Vec3(ux.X, ux.Y, ux.Z);
        var uy = br.Read3BD();
        UcsYDir = new Vec3(uy.X, uy.Y, uy.Z);
        UcsElevation = br.ReadBD();
        UcsOrthoView = br.ReadBS();
        if (HasShadePlot) ShadePlotMode = br.ReadBS();
        if (HasLightingBlock)
        {
            HasLighting = true;
            UseDefaultLights = br.ReadB();
            DefaultLightingType = br.ReadRC();
            Brightness = br.ReadBD();
            Contrast = br.ReadBD();
            AmbientColorIndex = DwgObjectReader.ReadCmc(
                br, out uint ambient, out string ambientName, out string ambientBook);
            AmbientTrueColor = ambient;
            AmbientColorName = ambientName;
            AmbientColorBookName = ambientBook;
        }
        TailEndBit = br.BitPosition;

        Kind = DwgEntityType.Viewport;
    }
}
