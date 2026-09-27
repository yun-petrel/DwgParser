using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;

namespace DwgParser.Cli;

public static partial class ProgramJsonExport
{
    internal static void WriteEntityCommon(System.Text.Json.Utf8JsonWriter w, DwgObjectRecord r,
        DwgEntity? ent, DwgVersion v, DwgObjectReader.WalkResult objs)
    {
        static void Null(System.Text.Json.Utf8JsonWriter w, string name) => w.WriteNull(name);
        void Slot(string name, bool onDisc, double val)
        {
            if (onDisc) ProgramRuntime.Num(w, name, val); else Null(w, name);
        }
        void SlotI(string name, bool onDisc, long val)
        {
            if (onDisc) w.WriteNumber(name, val); else Null(w, name);
        }
        void SlotB(string name, bool onDisc, bool val)
        {
            if (onDisc) w.WriteBoolean(name, val); else Null(w, name);
        }

        w.WriteStartObject("common");
        w.WriteNumber("entMode", r.EntMode);
        w.WriteBoolean("hasPreview", r.HasPreview);
        w.WriteNumber("previewSize", r.PreviewSize);
        w.WriteNumber("numReactors", r.NumReactors);
        SlotB("isXdicMissing", v >= DwgVersion.AC1018, r.IsXdicMissing);
        SlotB("hasDsData", v >= DwgVersion.AC1027, r.HasDsData);
        SlotB("noLinks", v <= DwgVersion.AC1015, r.NoLinks);
        w.WriteNumber("ltypeFlags", r.LtypeFlags);
        SlotI("plotStyleFlags", v >= DwgVersion.AC1015, r.PlotStyleFlags);
        SlotI("materialFlags", v >= DwgVersion.AC1021, r.MaterialFlags);
        SlotI("shadowFlags", v >= DwgVersion.AC1021, r.ShadowFlags);
        SlotB("hasFullVisualStyle", v >= DwgVersion.AC1024, r.HasFullVisualStyle);
        SlotB("hasFaceVisualStyle", v >= DwgVersion.AC1024, r.HasFaceVisualStyle);
        SlotB("hasEdgeVisualStyle", v >= DwgVersion.AC1024, r.HasEdgeVisualStyle);
        w.WriteNumber("invisibleRaw", r.Invisible);
        SlotI("lineWeightRaw", v >= DwgVersion.AC1015, r.LineWeight);
        Slot("linetypeScale", true, r.LinetypeScale);
        if (ent != null) w.WriteNumber("colorIndex", ent.ColorIndex);

        void Ref(string name, bool present, DwgParser.Core.IO.DwgHandleReference hr)
        {
            if (present) w.WriteString(name, ProgramRuntime.Hex(hr.Resolve(r.Handle)));
            else Null(w, name);
        }
        ulong? owner = ent?.OwnerHandle != 0 ? ent?.OwnerHandle
            : r.OwnerHandle != 0 ? r.OwnerHandle : null;
        if (owner is ulong oh) w.WriteString("owner", ProgramRuntime.Hex(oh));
        else Ref("owner", r.EntMode == 0, r.OwnerRef);
        w.WriteStartArray("reactors");
        foreach (var h in r.ReactorHandles) w.WriteStringValue(ProgramRuntime.Hex(h.Resolve(r.Handle)));
        w.WriteEndArray();
        Ref("xdic", r.HasXdicSlot, r.XdicRef);
        Ref("prev", r.HasLinks, r.PrevRef);
        Ref("next", r.HasLinks, r.NextRef);
        var layer = ent?.LayerHandle != 0 ? ent?.LayerHandle : null;
        if (layer is ulong lh) w.WriteString("layerHandle", ProgramRuntime.Hex(lh));
        else Ref("layerHandle", true, r.LayerRef);
        Ref("ltypeHandle", r.HasLtypeSlot, r.LtypeRef);
        if (r.HasLtypeSlot)
        {
            var ltName = objs.LtypeStyles.TryGetValue(r.LtypeRef.Resolve(r.Handle), out var li) &&
                         !string.IsNullOrEmpty(li.Name) ? li.Name : null;
            if (ltName is string ltn) w.WriteString("linetypeName", ltn);
            else Null(w, "linetypeName");
        }
        Ref("plotStyleHandle", r.HasPlotStyleSlot, r.PlotStyleRef);
        Ref("materialHandle", r.HasMaterialSlot, r.MaterialRef);
        Ref("shadowHandle", r.HasShadowSlot, r.ShadowRef);
        Ref("fullVisualStyleHandle", r.HasFullVisualStyle, r.FullVsRef);
        Ref("faceVisualStyleHandle", r.HasFaceVisualStyle, r.FaceVsRef);
        Ref("edgeVisualStyleHandle", r.HasEdgeVisualStyle, r.EdgeVsRef);
        w.WriteEndObject();
    }
}
