using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class MTextAnnotativeScale
{
    public short ClassVersion { get; set; }

    public bool DefaultFlag { get; set; }

    public short IgnoreAttachment { get; set; }

    public (double X, double Y, double Z) XAxisDir { get; set; }

    public (double X, double Y, double Z) InsPt { get; set; }

    public double RectWidth { get; set; }
    public double RectHeight { get; set; }
    public double ExtentsWidth { get; set; }
    public double ExtentsHeight { get; set; }

    public short ColumnType { get; set; }

    public int NumColumnHeights { get; set; }

    public double ColumnWidth { get; set; }
    public double Gutter { get; set; }
    public bool AutoHeight { get; set; }
    public bool FlowReversed { get; set; }

    public List<double> ColumnHeights { get; } = new();

    public static MTextAnnotativeScale? Read(BitReader br)
    {
        if (!br.ReadB())
            return null;

        var v = new MTextAnnotativeScale
        {
            ClassVersion = br.ReadBS(),
            DefaultFlag = br.ReadB(),
            IgnoreAttachment = br.ReadBS(),
            XAxisDir = br.Read3BD(),
            InsPt = br.Read3BD(),
            RectWidth = br.ReadBD(),
            RectHeight = br.ReadBD(),
            ExtentsWidth = br.ReadBD(),
            ExtentsHeight = br.ReadBD(),
            ColumnType = br.ReadBS(),
        };
        if (v.ColumnType != 0)
        {
            v.NumColumnHeights = br.ReadBL();
            v.ColumnWidth = br.ReadBD();
            v.Gutter = br.ReadBD();
            v.AutoHeight = br.ReadB();
            v.FlowReversed = br.ReadB();
            if (!v.AutoHeight && v.ColumnType == 2)
                for (int i = 0; i < v.NumColumnHeights && !br.EndOfStream; i++)
                    v.ColumnHeights.Add(br.ReadBD());
        }
        return v;
    }

    public void Write(BitWriter w)
    {
        w.WriteB(true);
        w.WriteBS(ClassVersion);
        w.WriteB(DefaultFlag);
        w.WriteBS(IgnoreAttachment);
        w.WriteBD(XAxisDir.X); w.WriteBD(XAxisDir.Y); w.WriteBD(XAxisDir.Z);
        w.WriteBD(InsPt.X); w.WriteBD(InsPt.Y); w.WriteBD(InsPt.Z);
        w.WriteBD(RectWidth);
        w.WriteBD(RectHeight);
        w.WriteBD(ExtentsWidth);
        w.WriteBD(ExtentsHeight);
        w.WriteBS(ColumnType);
        if (ColumnType == 0)
            return;
        w.WriteBL(NumColumnHeights);
        w.WriteBD(ColumnWidth);
        w.WriteBD(Gutter);
        w.WriteB(AutoHeight);
        w.WriteB(FlowReversed);
        if (!AutoHeight && ColumnType == 2)
            foreach (double h in ColumnHeights)
                w.WriteBD(h);
    }

    public static MTextAnnotativeScale FromMirror(DwgObjectRecord rec) => new()
    {
        ClassVersion = 4,
        DefaultFlag = true,
        IgnoreAttachment = rec.MTextAttachment,
        XAxisDir = rec.MTextXAxisDir,
        InsPt = rec.MTextInsPt,
        RectWidth = rec.MTextRectWidth,
        RectHeight = rec.MTextRectHeight,
        ExtentsWidth = rec.MTextExtentsWidth,
        ExtentsHeight = rec.MTextExtentsHeight,
        ColumnType = 0,
    };
}
