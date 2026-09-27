using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;

namespace DwgParser.Cli;

public static partial class Program
{

    private static void WriteLayers(System.Text.Json.Utf8JsonWriter w,
        DwgObjectReader.WalkResult objs)
    {
        w.WriteStartObject("layers");
        foreach (var layer in objs.Layers)
        {
            w.WriteStartObject(Hex(layer.Handle));
            w.WriteString("name", layer.Name);
            if (objs.LayerColors.TryGetValue(layer.Handle, out var col)) w.WriteNumber("color", col);
            Num(w, "dxf70", layer.Dxf70);
            w.WriteBoolean("frozen", layer.Frozen);
            w.WriteBoolean("off", layer.Off);
            w.WriteBoolean("frozenInNew", layer.FrozenInNew);
            w.WriteBoolean("locked", layer.Locked);
            if (layer.PlotFlag is bool pf) w.WriteBoolean("plotFlag", pf);
            if (layer.LineWeightRaw is int lwr) Num(w, "lineWeightRaw", lwr);
            w.WriteBoolean("xrefRef", layer.XrefRef);
            Num(w, "xrefResolved", layer.XrefResolved);
            w.WriteBoolean("xrefDep", layer.XrefDep);
            w.WriteEndObject();
        }
        w.WriteEndObject();
    }

    private static void WriteVports(System.Text.Json.Utf8JsonWriter w,
        DwgObjectReader.WalkResult objs)
    {
        w.WriteStartArray("vports");
        foreach (var r in objs.Records.OrderBy(x => x.Handle))
            if (r.Vport is { } vc && vc.Values is { } vv
                && (vc.Captured || vc.ValuesCommitted))
            {
                w.WriteStartObject();
                w.WriteString("handle", Hex(r.Handle));
                w.WriteString("name", vc.Name);
                w.WriteBoolean("captured", vc.Captured);
                w.WriteBoolean("valuesCommitted", vc.ValuesCommitted);
                Num(w, "viewSize", vv.ViewSize);
                Num(w, "viewWidth", vv.ViewWidth);
                Num(w, "aspectRatio", vv.AspectRatio);
                Pt(w, "viewCenter", vv.ViewCenter);
                Pt3(w, "viewTarget", vv.ViewTarget);
                Pt3(w, "viewDirection", vv.ViewDirection);
                Num(w, "twist", vv.Twist);
                Num(w, "lensLength", vv.LensLength);
                Num(w, "frontClipZ", vv.FrontClipZ);
                Num(w, "backClipZ", vv.BackClipZ);
                Num(w, "viewMode", vv.ViewMode);
                Num(w, "renderMode", vv.RenderMode);
                Pt(w, "lowerLeft", vv.LowerLeft);
                Pt(w, "upperRight", vv.UpperRight);
                w.WriteBoolean("ucsFollow", vv.UcsFollow);
                Num(w, "circleZoom", vv.CircleZoom);
                w.WriteBoolean("fastZoom", vv.FastZoom);
                Num(w, "ucsIcon", vv.UcsIcon);
                w.WriteBoolean("gridMode", vv.GridMode);
                Pt(w, "gridUnit", vv.GridUnit);
                w.WriteBoolean("snapMode", vv.SnapMode);
                w.WriteBoolean("snapStyle", vv.SnapStyle);
                Num(w, "snapIsoPair", vv.SnapIsoPair);
                Num(w, "snapAngle", vv.SnapAngle);
                Pt(w, "snapBase", vv.SnapBase);
                Pt(w, "snapUnit", vv.SnapUnit);
                w.WriteBoolean("ucsAtOrigin", vv.UcsAtOrigin);
                w.WriteBoolean("ucsVp", vv.UcsVp);
                Pt3(w, "ucsOrigin", vv.UcsOrigin);
                Pt3(w, "ucsXDir", vv.UcsXDir);
                Pt3(w, "ucsYDir", vv.UcsYDir);
                Num(w, "ucsElevation", vv.UcsElevation);
                Num(w, "ucsOrthoView", vv.UcsOrthoView);
                Num(w, "xrefResolved", vc.XrefResolved);
                if (vc.Captured)
                {
                    w.WriteNumber("lightsAnchorBit", vc.LightsAnchorBit);
                    w.WriteNumber("xrefStartBit", vc.XrefStartBit);
                    w.WriteNumber("visualStyleHandle", vc.VisualStyleHandle);
                }
                w.WriteBoolean("hasR2007aFields", vv.HasR2007aFields);
                if (vv.HasR2007aFields)
                {
                    w.WriteBoolean("useDefaultLights", vv.UseDefaultLights);
                    Num(w, "defaultLightingType", vv.DefaultLightingType);
                    Num(w, "brightness", vv.Brightness);
                    Num(w, "contrast", vv.Contrast);
                    Num(w, "ambientColorIndex", vv.AmbientIndex);
                    w.WriteNumber("ambientColorRgb", vv.AmbientRgb);
                    Num(w, "ambientColorFlag", vv.AmbientFlag);
                    Num(w, "gridFlags", vv.GridFlags);
                    Num(w, "gridMajor", vv.GridMajor);
                }
                w.WriteEndObject();
            }
        w.WriteEndArray();
    }

    private static void WriteBlocks(System.Text.Json.Utf8JsonWriter w,
        DwgObjectReader.WalkResult objs)
    {
        w.WriteStartArray("blocks");
        foreach (var b in objs.BlockRegions)
        {
            w.WriteStartObject();
            w.WriteString("handle", Hex(b.HeaderHandle));
            w.WriteString("name", string.IsNullOrEmpty(b.Name) ? b.NameGuess : b.Name);
            w.WriteBoolean("anonymous", b.IsAnonymous);
            w.WriteBoolean("anonymousDimBlock", b.IsAnonymousDimBlock);
            w.WriteNumber("entityCount", b.EntityCount);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void WriteTypeCounts(System.Text.Json.Utf8JsonWriter w,
        DwgDocument doc, DwgObjectReader.WalkResult objs)
    {
        w.WriteStartObject("typeCounts");
        foreach (var (t, n) in objs.TypeCounts.OrderByDescending(kv => kv.Value))
            w.WriteNumber(ResolveName(doc, t) + "/" + Hex(t), n);
        w.WriteEndObject();
    }

    private static void WriteRecords(System.Text.Json.Utf8JsonWriter w,
        DwgDocument doc, DwgObjectReader.WalkResult objs)
    {
        var entByHandle = new Dictionary<ulong, DwgEntity>();
        foreach (var e in objs.Entities) entByHandle[e.Handle] = e;
        w.WriteStartArray("records");
        foreach (var r in objs.Records)
        {
            w.WriteStartObject();
            w.WriteString("handle", Hex(r.Handle));
            w.WriteNumber("typeCode", r.TypeCode);
            w.WriteString("recordKind", r.Kind.ToString());
            w.WriteString("ownHandle", Hex(r.OwnHandle.Value));
            w.WriteNumber("bodyStart", r.BodyStart);
            w.WriteNumber("frameEnd", r.FrameEnd);
            if (r.Kind == DwgObjectRecordKind.Entity)
                WriteEntityCommon(w, r, entByHandle.TryGetValue(r.Handle, out var ent) ? ent : null,
                    doc.Version, objs);
            WriteXrecord(w, r.Xrecord);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}
