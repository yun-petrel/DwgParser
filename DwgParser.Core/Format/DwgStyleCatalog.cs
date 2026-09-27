using DwgParser.Core.Objects;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Format;

public sealed partial class DwgStyleCatalog
{
    private DwgStyleCatalog(DwgObjectReader.WalkResult objects)
    {
        Text = objects.StyleFonts;
        Dimension = objects.DimStyles;
        Linetype = objects.LtypeStyles;

        var visual = new Dictionary<ulong, IVisualStyle>();
        var table = new Dictionary<ulong, ITableStyle>();
        var mleader = new Dictionary<ulong, IMLeaderStyle>();
        var mline = new Dictionary<ulong, IMLineStyle>();
        var view = new Dictionary<ulong, IViewStyle>();
        var viewTemplate = new Dictionary<ulong, IViewStyleTemplate>();

        foreach (var record in objects.Records)
        {
            Add(visual, record.VisualStyleModel);
            Add(table, record.TableStyle);
            Add(mleader, record.MLeader);
            Add(mline, record.MLineStyle);
            Add(view, record.ViewStyle);
            Add(viewTemplate, record.ViewStyle);
        }

        Visual = visual;
        TableStyle = table;
        MLeaderStyle = mleader;
        MLineStyle = mline;
        ViewStyle = view;
        ViewStyleTemplate = viewTemplate;

        All = Text.Values.Cast<IRecord>()
            .Concat(Dimension.Values)
            .Concat(Linetype.Values)
            .Concat(Visual.Values)
            .Concat(TableStyle.Values)
            .Concat(MLeaderStyle.Values)
            .Concat(MLineStyle.Values)
            .Concat(ViewStyle.Values)
            .Concat(ViewStyleTemplate.Values)
            .GroupBy(static style => style.Handle)
            .Select(static group => group.First())
            .OrderBy(static style => style.Handle)
            .ToArray();
    }

    public IReadOnlyDictionary<ulong, DwgObjectReader.TextStyleInfo> Text { get; }

    public IReadOnlyDictionary<ulong, DwgObjectReader.DimStyleInfo> Dimension { get; }

    public IReadOnlyDictionary<ulong, DwgObjectReader.LinetypeInfo> Linetype { get; }

    public IReadOnlyDictionary<ulong, IVisualStyle> Visual { get; }

    public IReadOnlyDictionary<ulong, ITableStyle> TableStyle { get; }

    public IReadOnlyDictionary<ulong, IMLeaderStyle> MLeaderStyle { get; }

    public IReadOnlyDictionary<ulong, IMLineStyle> MLineStyle { get; }

    public IReadOnlyDictionary<ulong, IViewStyle> ViewStyle { get; }

    public IReadOnlyDictionary<ulong, IViewStyleTemplate> ViewStyleTemplate { get; }

    public IReadOnlyList<IRecord> All { get; }

    private static void Add<T>(Dictionary<ulong, T> index, T? style) where T : class, IRecord
    {
        if (style is null || style.Handle == 0) return;
        index.TryAdd(style.Handle, style);
    }

    internal static DwgStyleCatalog From(DwgObjectReader.WalkResult objects)
        => new(objects);
}
