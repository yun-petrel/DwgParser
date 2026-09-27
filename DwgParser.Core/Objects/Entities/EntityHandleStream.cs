using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public interface IEntityHeadFlags
{
    byte EntMode { get; }

    uint NumReactors { get; }

    bool NoLinks { get; }

    bool IsXdicMissing { get; }

    byte LtypeFlags { get; }

    byte PlotStyleFlags { get; }

    byte MaterialFlags { get; }

    byte ShadowFlags { get; }

    bool HasFullVisualStyle { get; }
    bool HasFaceVisualStyle { get; }
    bool HasEdgeVisualStyle { get; }
}

public interface IEntityHandleStream
{
    void Read(BitReader hdl, DwgEntity e, DwgObjectRecord? record, IEntityHeadFlags flags);
}

public abstract class EntityHandleStream : IEntityHandleStream
{
    protected abstract bool R2000Family { get; }

    protected abstract bool HasMaterialShadowSlots { get; }

    protected abstract bool HasVisualStyleSlots { get; }

    public void Read(BitReader hdl, DwgEntity e, DwgObjectRecord? record, IEntityHeadFlags f)
    {
        if (f.EntMode == 0)
        {
            var owner = hdl.ReadHandle();
            e.OwnerHandle = owner.Resolve(e.Handle);
            if (record != null) record.OwnerRef = owner;
        }
        for (uint i = 0; i < Math.Min(f.NumReactors, 10000u); i++)
        {
            var rh = hdl.ReadHandle();
            record?.ReactorHandles.Add(rh);
        }
        if (R2000Family || !f.IsXdicMissing)
        {
            var xdic = hdl.ReadHandle();
            if (record != null) { record.HasXdicSlot = true; record.XdicRef = xdic; }
        }
        if (R2000Family)
            ReadLinks(hdl, record, f);
        var layer = hdl.ReadHandle();
        e.LayerHandle = layer.Resolve(e.Handle);
        if (record != null) record.LayerRef = layer;
        if (f.LtypeFlags == 3)
        {
            var lt = hdl.ReadHandle();
            if (record != null) { record.HasLtypeSlot = true; record.LtypeRef = lt; }
        }
        if (HasMaterialShadowSlots)
        {
            if (f.MaterialFlags == 3)
            {
                var m = hdl.ReadHandle();
                if (record != null) { record.HasMaterialSlot = true; record.MaterialRef = m; }
            }
            if (f.ShadowFlags == 3)
            {
                var sh = hdl.ReadHandle();
                if (record != null) { record.HasShadowSlot = true; record.ShadowRef = sh; }
            }
        }
        if (f.PlotStyleFlags == 3)
        {
            var ps = hdl.ReadHandle();
            if (record != null) { record.HasPlotStyleSlot = true; record.PlotStyleRef = ps; }
        }
        if (HasVisualStyleSlots)
        {
            if (f.HasFullVisualStyle)
            {
                var fv = hdl.ReadHandle();
                if (record != null) record.FullVsRef = fv;
            }
            if (f.HasFaceVisualStyle)
            {
                var fv = hdl.ReadHandle();
                if (record != null) record.FaceVsRef = fv;
            }
            if (f.HasEdgeVisualStyle)
            {
                var ev = hdl.ReadHandle();
                if (record != null) record.EdgeVsRef = ev;
            }
        }
    }

    private void ReadLinks(BitReader hdl, DwgObjectRecord? record, IEntityHeadFlags f)
    {
        if (f.NoLinks) return;
        if (record != null)
        {
            record.HasLinks = true;
            record.PrevRef = hdl.ReadHandle();
            record.NextRef = hdl.ReadHandle();
        }
        else
        {
            hdl.ReadHandle();
            hdl.ReadHandle();
        }
    }
}

internal sealed class EntityHandleStreamAcR2000 : EntityHandleStream
{
    protected override bool R2000Family => true;
    protected override bool HasMaterialShadowSlots => false;
    protected override bool HasVisualStyleSlots => false;
}

internal sealed class EntityHandleStreamAcR2004 : EntityHandleStream
{
    protected override bool R2000Family => false;
    protected override bool HasMaterialShadowSlots => false;
    protected override bool HasVisualStyleSlots => false;
}

internal sealed class EntityHandleStreamAcR2007 : EntityHandleStream
{
    protected override bool R2000Family => false;
    protected override bool HasMaterialShadowSlots => true;
    protected override bool HasVisualStyleSlots => false;
}

internal sealed class EntityHandleStreamAcR2010Plus : EntityHandleStream
{
    protected override bool R2000Family => false;
    protected override bool HasMaterialShadowSlots => true;
    protected override bool HasVisualStyleSlots => true;
}
