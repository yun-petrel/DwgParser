using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class MTextEntity : DwgEntity, IMText
{

    short IMText.Attachment => TextAttachment;

    public long BgFillScale { get; private set; }

    public long BgFillTrans { get; private set; }

    public bool LineSpacingUnknownBit { get; private set; }

    internal MTextAnnotativeScale? Annotative { get; private set; }

    protected abstract bool HasRectHeight { get; }

    protected abstract bool HasLineSpacing { get; }

    protected abstract bool HasBackgroundFill { get; }

    protected abstract bool HasAnnotativeData { get; }

    public void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        var ins = br.Read3BD();
        var extrusion = br.Read3BD();
        var xAxisDir = br.Read3BD();
        double rectWidth = br.ReadBD();
        double rectHeight = 0.0;
        if (HasRectHeight) rectHeight = br.ReadBD();
        double height = br.ReadBD();
        short attachment = br.ReadBS();
        short flowDir = br.ReadBS();
        double extentsH = br.ReadBD();
        double extentsW = br.ReadBD();
        long txtStart = br.BitPosition;
        string text = br.ReadTV();
        record?.CaptureString(txtStart, br.BitPosition, text);
        if (record != null)
        {
            record.HasMTextPrefix = true;
            record.MTextInsPt = ins;
            record.MTextExtrusion = extrusion;
            record.MTextXAxisDir = xAxisDir;
            record.MTextRectWidth = rectWidth;
            record.MTextRectHeight = rectHeight;
            record.MTextTextHeight = height;
            record.MTextAttachment = attachment;
            record.MTextFlowDir = flowDir;
            record.MTextExtentsHeight = extentsH;
            record.MTextExtentsWidth = extentsW;
            record.MTextTailStartBit = br.BitPosition;
        }
        short lsStyle = 0;
        double lsFactor = 1.0;
        if (HasLineSpacing)
        {
            lsStyle = br.ReadBS();
            lsFactor = br.ReadBD();
            LineSpacingUnknownBit = br.ReadB();
        }

        if (HasBackgroundFill)
        {
            long bgFlag = br.ReadBL0();
            if ((bgFlag & 1) != 0)
            {
                BgFillScale = br.ReadBL();
                ReadBackgroundFillColor(br);
                BgFillTrans = br.ReadBL();
            }
        }
        if (HasAnnotativeData)
        {
            br.ReadB();
            long annStart = br.BitPosition;
            Annotative = MTextAnnotativeScale.Read(br);
            if (record != null && Annotative != null)
            {
                record.MTextAnnotativeStartBit = annStart;
                record.MTextAnnotativeEndBit = br.BitPosition;
            }
        }

        CommitMText(ins, extrusion, xAxisDir, height, attachment, rectWidth, rectHeight, lsStyle, lsFactor, text);
    }

    private static void ReadBackgroundFillColor(BitReader br) =>
        DwgObjectReader.ReadEncColor(br, new DwgEntity());

    private void CommitMText((double X, double Y, double Z) ins, (double X, double Y, double Z) extrusion,
        (double X, double Y, double Z) xAxisDir, double height, short attachment, double rectWidth,
        double rectHeight, short lsStyle, double lsFactor, string text)
    {
        Kind = DwgEntityType.MText;
        Start = new Vec2(ins.X, ins.Y);
        Height = height;
        TextValue = text;
        RectWidth = rectWidth;
        RectHeight = HasRectHeight ? rectHeight : 0.0;
        TextAttachment = attachment;
        ExtrusionZ = extrusion.Z;
        LineSpacingStyle = lsStyle;
        LineSpacingFactor = lsFactor;
        if (xAxisDir.X != 0 || xAxisDir.Y != 0)
            Rotation = System.Math.Atan2(xAxisDir.Y, xAxisDir.X);
    }
}
