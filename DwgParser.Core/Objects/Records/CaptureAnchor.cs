namespace DwgParser.Core.Objects.Records;

public static class CaptureAnchor
{
    public static long TryGet(DwgObjectRecord r)
    {
        if (r is null) return 0;
        var c = r.VisualStyle;
        if (c is not null && c.HsBit > 0) return c.HsBit;
        if (Try(r.DimAssoc, static x => x.HsBit, out long v)) return v;
        if (Try(r.DimStyle, static x => x.HsBit, out v)) return v;
        if (Try(r.ImageDef, static x => x.HsBit, out v)) return v;
        if (Try(r.Material, static x => x.HsBit, out v)) return v;
        if (Try(r.DataLink, static x => x.HsBit, out v)) return v;
        if (r.Xrecord is { } xr && xr.DataEndBit > 0) return xr.DataEndBit;
        if (r.SortentsTable is { } st && st.WalkEndBit > 0) return st.WalkEndBit;
        return 0;
    }

    private static bool Try<T>(T? cap, System.Func<T, long> pick, out long value)
        where T : class
    {
        value = cap is null ? 0 : pick(cap);
        return value > 0;
    }
}
