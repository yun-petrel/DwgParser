using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class InsertEntity : DwgEntity, IInsert
{
    protected override string WriteSvg(RenderContext ctx, Matrix m)
        => EntitySvg.RenderInsert(this, ctx, m, 0);

    protected abstract void ReadScale(BitReader br);

    protected virtual bool ReadsNumOwned => false;

    public bool ReadGeometry(BitReader br, DwgObjectRecord? record)
    {
        var ins = br.Read3BD();
        ReadScale(br);
        double rotation = br.ReadBD();
        var extrusion = br.Read3BD();
        bool hasAttribs = br.ReadB();
        if (record != null)
            record.InsertAfterAttribsBit = br.BitPosition;
        if (hasAttribs && ReadsNumOwned)
            ReadNumOwned(br, record);

        Kind = DwgEntityType.Insert;
        Start = new Vec2(ins.X, ins.Y);
        Elevation = ins.Z;
        ExtrusionZ = extrusion.Z;
        HasAttributes = hasAttribs;
        Rotation = rotation;
        return hasAttribs;
    }

    protected static void ReadNumOwned(BitReader br, DwgObjectRecord? record)
    {
        long numOwnedStart = br.BitPosition;
        uint numOwned = (uint)br.ReadBL();
        if (record != null)
        {
            record.NumOwned = numOwned;
            record.InsertNumOwnedBitLen = (int)(br.BitPosition - numOwnedStart);
        }
    }

    protected void ReadScaleFlag(BitReader br)
    {
        byte scaleFlag = br.ReadBB();
        double sx = 1.0, sy = 1.0, sz = 1.0;
        switch (scaleFlag)
        {
            case 0:
                sx = br.ReadRD();
                sy = br.ReadDD(sx);
                sz = br.ReadDD(sx);
                break;
            case 1:
                sy = br.ReadDD(1.0);
                sz = br.ReadDD(1.0);
                break;
            case 2:
                sx = br.ReadRD();
                sy = sx;
                sz = sx;
                break;
            case 3:
                break;
        }
        ScaleX = sx;
        ScaleY = sy;
        ScaleZ = sz;
    }
}
