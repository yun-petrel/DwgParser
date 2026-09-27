using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;

namespace DwgParser.Cli;

public static partial class Program
{

    private static void WriteEntities(System.Text.Json.Utf8JsonWriter w,
        DwgObjectReader.WalkResult objs)
    {
        w.WriteStartArray("entities");
        foreach (var e in objs.Entities)
        {
            w.WriteStartObject();
            w.WriteString("handle", Hex(e.Handle));
            w.WriteString("kind", e.Kind.ToString());
            w.WriteNumber("color", e.ColorIndex);
            w.WriteNumber("entMode", e.EntMode);
            w.WriteString("owner", Hex(e.OwnerHandle));
            w.WriteBoolean("isErased", e.IsErased);
            w.WriteString("layerHandle", Hex(e.LayerHandle));
            Num(w, "extrusionZ", e.ExtrusionZ);
            Num(w, "thickness", e.Thickness);
            Num(w, "elevation", e.Elevation);
            Pt(w, "start", e.Start);
            Pt(w, "end", e.End);
            Num(w, "startZ", e.StartZ);
            Num(w, "endZ", e.EndZ);
            Pt(w, "center", e.Center);
            Num(w, "radius", e.Radius);
            Num(w, "startAngle", e.StartAngle);
            Num(w, "endAngle", e.EndAngle);
            w.WriteString("text", e.TextValue);
            Num(w, "height", e.Height);
            Num(w, "rotation", e.Rotation);
            Num(w, "widthFactor", e.WidthFactor);
            Num(w, "constantWidth", e.ConstantWidth);
            Num(w, "scaleX", e.ScaleX);
            Num(w, "scaleY", e.ScaleY);
            Num(w, "scaleZ", e.ScaleZ);
            if (e is IPoint pt) Num(w, "pointAngle", pt.PointAngle);
            w.WriteString("blockHeaderHandle", Hex(e.BlockHeaderHandle));
            w.WriteString("name", e.Name);
            w.WriteBoolean("hasAttribs", e.HasAttributes);
            w.WriteBoolean("solidFill", e.SolidFill);
            if (e.AcisData is { Length: > 0 } acis)
            {
                w.WriteNumber("acisDataLen", acis.Length);
                if (e.AcisVertices is { Length: > 0 } vs)
                {
                    w.WriteNumber("acisVertexCount", vs.Length);
                    w.WriteStartArray("acisVertices");
                    foreach (var v in vs)
                    {
                        w.WriteStartObject();
                        w.WriteNumber("x", v.X); w.WriteNumber("y", v.Y); w.WriteNumber("z", v.Z);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
            }
            w.WriteBoolean("closed", e.Closed);
            w.WriteBoolean("plineGen", e.PlineGen);
            w.WriteString("tag", e.Tag);
            w.WriteString("prompt", e.Prompt);
            w.WriteNumber("attFlags", e.AttFlags);
            w.WriteBoolean("attInvisible", (e.AttFlags & 1) != 0);
            w.WriteBoolean("attConstant", (e.AttFlags & 2) != 0);
            w.WriteBoolean("attVerifiable", (e.AttFlags & 4) != 0);
            w.WriteBoolean("attPreset", (e.AttFlags & 8) != 0);
            w.WriteNumber("attFieldLength", e.AttFieldLength);
            w.WriteNumber("textAttachment", e.TextAttachment);
            Num(w, "rectWidth", e.RectWidth);
            Num(w, "rectHeight", e.RectHeight);
            Num(w, "lineSpacingStyle", e.LineSpacingStyle);
            Num(w, "lineSpacingFactor", e.LineSpacingFactor);
            if (e is MTextEntity mtU)
                w.WriteBoolean("mtextLineSpacingUnknownBit", mtU.LineSpacingUnknownBit);
            if (e is ModelerGeometryEntity mgU && !mgU.AcisEmpty)
                w.WriteBoolean("acisUnknownBit", mgU.AcisUnknownBit);
            Pt(w, "alignmentPoint", e.AlignmentPoint);
            Num(w, "obliqueAngle", e.ObliqueAngle);
            Num(w, "textGenerationFlags", e.TextGenerationFlags);
            w.WriteBoolean("textMirroredX", (e.TextGenerationFlags & 2) != 0);
            w.WriteBoolean("textMirroredY", (e.TextGenerationFlags & 4) != 0);
            if (e is ILeader ld)
            {
                Num(w, "leaderAnnotType", ld.LeaderAnnotType);
                Num(w, "leaderPathType", ld.LeaderPathType);
                Num(w, "leaderBoxHeight", ld.LeaderBoxHeight);
                Num(w, "leaderBoxWidth", ld.LeaderBoxWidth);
                Pt3(w, "leaderOrigin", ld.LeaderOrigin);
                Pt3(w, "leaderExtrusion", ld.LeaderExtrusion);
                Pt3(w, "leaderXDirection", ld.LeaderXDirection);
                Pt3(w, "leaderInsertionOffset", ld.LeaderInsertionOffset);
                Pt3(w, "leaderEndPointProjection", ld.LeaderEndPointProjection);
                w.WriteBoolean("leaderHooklineDir", ld.LeaderHooklineDir);
                w.WriteBoolean("leaderArrowheadOn", ld.LeaderArrowheadOn);
                Num(w, "leaderArrowheadType", ld.LeaderArrowheadType);
                w.WriteBoolean("leaderUnknownBit1", ld.LeaderUnknownBit1);
                w.WriteBoolean("leaderTailUnknownBit4", ld.LeaderTailUnknownBit4);
                w.WriteBoolean("leaderTailUnknownBit5", ld.LeaderTailUnknownBit5);
            }
            if (e is DimensionEntity de)
            {
                Num(w, "leaderLength", de.LeaderLength);
                Num(w, "ordinateAxisFlag", de.OrdinateAxisFlag);
                Num(w, "dimLspaceStyle", de.DimLspaceStyle);
                Num(w, "dimLspaceFactor", de.DimLspaceFactor);
                Num(w, "dimTextFlags", de.DimensionTextFlags);
                w.WriteStartArray("dimTextFlagBits");
                NumValue(w, (de.DimensionTextFlags & 1)); NumValue(w, (de.DimensionTextFlags >> 1) & 1);
                NumValue(w, (de.DimensionTextFlags >> 2) & 1); NumValue(w, (de.DimensionTextFlags >> 3) & 1);
                w.WriteEndArray();
                Pt(w, "dimCloneInsPt", de.DimensionCloneInsPt);
                w.WriteStartArray("dimInsScale");
                NumValue(w, de.DimInsScale.X); NumValue(w, de.DimInsScale.Y); NumValue(w, de.DimInsScale.Z);
                w.WriteEndArray();
            }
            if (e is IVertex2D vx)
            {
                Num(w, "vertexFlag", vx.Flag);
                Num(w, "vertexStartWidth", vx.StartWidth);
                Num(w, "vertexEndWidth", vx.EndWidth);
                Num(w, "vertexTangentDir", vx.TangentDir);
            }
            if (e is IPolyline2D p2)
            {
                Num(w, "curveType", p2.CurveType);
                Num(w, "plineStartWidth", p2.StartWidth);
                Num(w, "plineEndWidth", p2.EndWidth);
            }
            if (e is IFace fe) Num(w, "faceInvisFlags", fe.InvisFlags);
            if (e is HatchEntity he)
            {
                w.WriteBoolean("hatchGradient", he.IsGradientFill);
                Num(w, "hatchGradientAngle", he.GradientAngle);
                Num(w, "hatchGradientShift", he.GradientShift);
                w.WriteBoolean("hatchGradientSingleColor", he.GradientSingleColor);
                Num(w, "hatchGradientTint", he.GradientTint);
                Num(w, "hatchGradientColorCount", he.GradientColorCount);
                w.WriteBoolean("hatchAssociative", he.IsAssociative);
                w.WriteStartArray("hatchPathFlags");
                foreach (var pf in he.PathFlags) NumValue(w, pf);
                w.WriteEndArray();
                w.WriteStartArray("hatchArcCcw");
                foreach (var v in he.ArcCcwFlags) w.WriteBooleanValue(v);
                w.WriteEndArray();
                w.WriteStartArray("hatchEllipseCcw");
                foreach (var v in he.EllipseCcwFlags) w.WriteBooleanValue(v);
                w.WriteEndArray();
                w.WriteStartArray("hatchSplines");
                foreach (var s in he.Splines)
                {
                    w.WriteStartObject();
                    w.WriteNumber("degree", s.Degree);
                    w.WriteBoolean("rational", s.Rational);
                    w.WriteBoolean("periodic", s.Periodic);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteString("hatchPatternName", he.PatternName);
                Num(w, "hatchStyle", he.Style);
                Num(w, "hatchPatternType", he.PatternType);
                if (he.PatternAngle is { } pa) Num(w, "hatchPatternAngle", pa);
                if (he.PatternScale is { } ps) Num(w, "hatchPatternScale", ps);
                if (he.PatternDouble is { } pd) w.WriteBoolean("hatchPatternDouble", pd);
                Num(w, "hatchPatternLineCount", he.PatternLines.Count);
                if (he.PatternLines.Count > 0)
                {
                    w.WriteStartArray("hatchPatternLines");
                    foreach (var ln in he.PatternLines)
                    {
                        w.WriteStartObject();
                        Num(w, "angle", ln.Angle);
                        w.WriteStartArray("base");
                        NumValue(w, ln.Base.X); NumValue(w, ln.Base.Y);
                        w.WriteEndArray();
                        w.WriteStartArray("offset");
                        NumValue(w, ln.Offset.X); NumValue(w, ln.Offset.Y);
                        w.WriteEndArray();
                        w.WriteStartArray("dashes");
                        foreach (var dv in ln.Dashes) NumValue(w, dv);
                        w.WriteEndArray();
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                if (he.PixelSize is { } psz) Num(w, "hatchPixelSize", psz);
                Num(w, "hatchSeedCount", he.SeedPoints.Count);
                w.WriteStartArray("hatchSeedPoints");
                foreach (var seed in he.SeedPoints)
                {
                    w.WriteStartObject();
                    Num(w, "x", seed.X);
                    Num(w, "y", seed.Y);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            if (e is ViewportEntity ve)
            {
                Num(w, "vportLensLength", ve.LensLength);
                Num(w, "vportCircleZoom", ve.CircleZoom);
                Num(w, "vportGridMajor", ve.GridMajor);
                w.WriteStartArray("vportSnapBase"); NumValue(w, ve.SnapBase.X); NumValue(w, ve.SnapBase.Y); w.WriteEndArray();
                w.WriteStartArray("vportSnapUnit"); NumValue(w, ve.SnapUnit.X); NumValue(w, ve.SnapUnit.Y); w.WriteEndArray();
                w.WriteStartArray("vportGridUnit"); NumValue(w, ve.GridUnit.X); NumValue(w, ve.GridUnit.Y); w.WriteEndArray();
                Num(w, "vportNumFrozenLayers", ve.NumFrozenLayers);
                Num(w, "vportStatusFlag", ve.StatusFlag);
                w.WriteString("vportStyleSheet", ve.StyleSheet);
                Num(w, "vportRenderMode", ve.RenderMode);
                w.WriteBoolean("vportUcsAtOrigin", ve.UcsAtOrigin);
                w.WriteBoolean("vportUcsVp", ve.UcsVp);
                Pt3(w, "vportUcsOrigin", ve.UcsOrigin);
                Pt3(w, "vportUcsXDir", ve.UcsXDir);
                Pt3(w, "vportUcsYDir", ve.UcsYDir);
                Num(w, "vportUcsElevation", ve.UcsElevation);
                Num(w, "vportUcsOrthoView", ve.UcsOrthoView);
                Num(w, "vportShadePlotMode", ve.ShadePlotMode);
                w.WriteBoolean("vportHasLighting", ve.HasLighting);
                if (ve.HasLighting)
                {
                    w.WriteBoolean("vportUseDefaultLights", ve.UseDefaultLights);
                    Num(w, "vportDefaultLightingType", ve.DefaultLightingType);
                    Num(w, "vportBrightness", ve.Brightness);
                    Num(w, "vportContrast", ve.Contrast);
                    Num(w, "vportAmbientColorIndex", ve.AmbientColorIndex);
                }
            }
            if (e is IMText mt)
            {
                Num(w, "mtextBgFillScale", mt.BgFillScale);
                Num(w, "mtextBgFillTrans", mt.BgFillTrans);
            }
            Num(w, "horizMode", e.HorizMode);
            Num(w, "vertMode", e.VertMode);
            w.WriteString("textStyleHandle", Hex(e.TextStyleHandle));
            Num(w, "vportWidth", e.VportWidth);
            Pt(w, "vportViewCenter", e.VportViewCenter);
            Num(w, "vportViewSize", e.VportViewSize);
            Num(w, "vportTwist", e.VportTwist);
            Num(w, "vportSnapAngle", e.VportSnapAngle);
            Pt(w, "vportViewTarget", e.VportViewTarget);
            Pt(w, "vportViewDirection", e.VportViewDirection);
            Num(w, "vportFrontClipZ", e.VportFrontClipZ);
            Num(w, "vportBackClipZ", e.VportBackClipZ);
            Num(w, "tableColumns", (int)e.TableColumns);
            Num(w, "tableRows", (int)e.TableRows);
            if (e is TableEntity te)
            {
                Num(w, "tableFlag", (int)te.TableFlag);
                if (te.HorizDirection is { } hd)
                { w.WriteStartArray("tableHorizDirection"); NumValue(w, hd.X); NumValue(w, hd.Y); w.WriteEndArray(); }
                w.WriteStartArray("tableColWidths");
                foreach (var cwv in te.ColWidths) NumValue(w, cwv);
                w.WriteEndArray();
                w.WriteStartArray("tableRowHeights");
                foreach (var rhv in te.RowHeights) NumValue(w, rhv);
                w.WriteEndArray();
                w.WriteStartArray("tableCells");
                foreach (var c in te.Cells)
                {
                    w.WriteStartObject();
                    Num(w, "type", c.Type);
                    if (c.Text.Length > 0) w.WriteString("text", c.Text);
                    if (c.FormattedValue is { Length: > 0 } fv && fv != c.Text) w.WriteString("formattedValue", fv);
                    if (c.Tooltip is { Length: > 0 } tt) w.WriteString("tooltip", tt);
                    if (c.Alignment is short al) Num(w, "alignment", (int)al);
                    if (c.TextHeight is double th) Num(w, "textHeight", th);
                    if (c.EdgeFlags != 0) Num(w, "edgeFlags", (int)c.EdgeFlags);
                    if (c.CellFlagOverride != 0) Num(w, "cellFlagOverride", c.CellFlagOverride);
                    if (c.VirtualEdgeFlag != 0) Num(w, "virtualEdgeFlag", (int)c.VirtualEdgeFlag);
                    if (c.ValueDataType != 0)
                    {
                        Num(w, "valueDataType", c.ValueDataType);
                        Num(w, "valueFormatFlags", c.ValueFormatFlags);
                        Num(w, "valueUnitType", c.ValueUnitType);
                    }
                    if (c.Rotation != 0) Num(w, "rotation", c.Rotation);
                    if (c.IsMergedValue || c.MergedWidth != 1 || c.MergedHeight != 1)
                    { w.WriteBoolean("merged", c.IsMergedValue); Num(w, "mergedWidth", c.MergedWidth); Num(w, "mergedHeight", c.MergedHeight); }
                    if (c.Type == 2) Num(w, "blockScale", c.BlockScale);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                if (te.StyleOverride is { } sov)
                {
                    w.WriteStartObject("tableStyleOverride");
                    Num(w, "flag", sov.Flag);
                    if (sov.TitleSuppressed is bool ts) w.WriteBoolean("titleSuppressed", ts);
                    if (sov.FlowDirection is short fd) Num(w, "flowDirection", (int)fd);
                    if (sov.HorzCellMargin is double hm) Num(w, "horzCellMargin", hm);
                    if (sov.VertCellMargin is double vm) Num(w, "vertCellMargin", vm);
                    if (sov.DataRowHeight is double drh) Num(w, "dataRowHeight", drh);
                    w.WriteEndObject();
                }
            }
            if (e is MLineEntity mle)
            {
                Num(w, "mlineScale", mle.Scale);
                Num(w, "mlineJustification", mle.Justification);
                Num(w, "mlineFlags", mle.Flags);
                Num(w, "mlineLineCount", mle.LineCount);
                w.WriteNumber("mlineVertexCount", mle.VertexCount);
                w.WriteString("mlineStyleHandle", Hex(mle.MLineStyleHandle));
            }
            if (e is ToleranceEntity tole)
            {
                Num(w, "tolDirX", tole.DirX);
                Num(w, "tolDirY", tole.DirY);
                Num(w, "tolDirZ", tole.DirZ);
            }
            Num(w, "measurement", e.Measurement);
            Pt(w, "dimTextMid", e.DimTextMid);
            Pt(w, "dimXLine1", e.DimXLine1);
            Pt(w, "dimXLine2", e.DimXLine2);
            Pt(w, "dimDefPoint", e.DimDefPoint);
            Pt(w, "dimDefPoint2", e.DimDefPoint2);
            Pt(w, "dimLinePoint", e.DimLinePoint);
            Pt(w, "dimCenter", e.DimCenter);
            w.WriteString("dimBlockHandle", Hex(e.DimBlockHandle));
            w.WriteString("dimSubtype", e.DimSubtype);
            w.WriteString("ltype", e.LtypeName);
            w.WriteStartArray("vertexStartWidths");
            foreach (var wv in e.VertexStartWidths) NumValue(w, wv);
            w.WriteEndArray();
            w.WriteStartArray("vertexEndWidths");
            foreach (var wv in e.VertexEndWidths) NumValue(w, wv);
            w.WriteEndArray();
            w.WriteStartArray("points");
            foreach (var p in e.Points) { w.WriteStartArray(); NumValue(w, p.X); NumValue(w, p.Y); w.WriteEndArray(); }
            w.WriteEndArray();
            if (e is MLeaderEntity ml)
            {
                Num(w, "mleaderScale", ml.ContextScale);
                Num(w, "mleaderTextHeight", ml.MTextHeight);
                Num(w, "mleaderArrowSize", ml.CtxArrowSize);
                Num(w, "mleaderLandingGap", ml.LandingGap);
                Num(w, "mleaderDoglegLength", ml.DoglegLength);
                w.WriteBoolean("mleaderHasLanding", ml.HasLanding);
                w.WriteBoolean("mleaderHasDogleg", ml.HasDogleg);
                w.WriteBoolean("mleaderHasContent", ml.HasTextContent || ml.HasBlockContent);
                w.WriteString("mleaderContentType", ml.ContentType);
                w.WriteString("mleaderContentText", ml.ContentText);
                w.WriteNumber("mleaderLeaderCount", ml.LeaderCount);
                w.WriteNumber("mleaderVertexCount", ml.VertexCount);
                w.WriteNumber("mleaderClassVersion", ml.ClassVersion);
                Num(w, "mleaderScaleFactor", ml.ScaleFactor);
                w.WriteBoolean("mleaderCaptured", ml.Captured);
                w.WriteString("mleaderStyleHandle", Hex(ml.MLeaderStyleHandle));
                Num(w, "mleaderTopLineWeight", ml.TopLineWeight);
                w.WriteBoolean("mleaderNormalReversed", ml.IsNormalReversed);
                w.WriteBoolean("mleaderNegTextDir", ml.IsNegTextDir);
                Num(w, "mleaderLineSpacingFactor", ml.MTextLineSpacingFactor);
                Num(w, "mleaderLineSpacingStyle", ml.MTextLineSpacingStyle);
                Num(w, "mleaderTextAlignment", ml.MTextAlignment);
                Num(w, "mleaderTextFlow", ml.MTextFlow);
                Num(w, "mleaderBgScale", ml.MTextBgScale);
                Num(w, "mleaderBgTransparency", ml.MTextBgTransparency);
                w.WriteBoolean("mleaderIsBgFill", ml.MTextIsBgFill);
                w.WriteBoolean("mleaderIsBgMaskFill", ml.MTextIsBgMaskFill);
                Num(w, "mleaderColType", ml.MTextColType);
                w.WriteBoolean("mleaderHeightAuto", ml.MTextIsHeightAuto);
                Num(w, "mleaderColWidth", ml.MTextColWidth);
                Num(w, "mleaderColGutter", ml.MTextColGutter);
                w.WriteBoolean("mleaderColFlowReversed", ml.MTextIsColFlowReversed);
                w.WriteBoolean("mleaderWordBreak", ml.MTextWordBreak);
                w.WriteBoolean("mleaderMTextUnknownTailBit", ml.MTextUnknownTailBit);
                Num(w, "mleaderBlockRotation", ml.BlockRotation);
            }
            if (e is SplineEntity sp)
            {
                w.WriteNumber("splineScenario", sp.Scenario);
                w.WriteNumber("splineDegree", sp.Degree);
                w.WriteBoolean("splineNurbs", sp.IsNurbsBranch);
                w.WriteNumber("splineKnotCount", sp.KnotCount);
                w.WriteNumber("splineCtrlCount", sp.Points.Count);
                w.WriteNumber("splineFitCount", sp.FitPoints.Count);
                w.WriteBoolean("splineRational", sp.Rational);
                w.WriteBoolean("splineClosed", sp.Closed);
                w.WriteBoolean("splinePeriodic", sp.Periodic);
                Num(w, "splineKnotParam", sp.KnotParam);
            }
            w.WriteStartArray("bulges");
            foreach (var b in e.Bulges) NumValue(w, b);
            w.WriteEndArray();
            w.WriteStartArray("corners");
            foreach (var c in e.Corners) { w.WriteStartArray(); NumValue(w, c.X); NumValue(w, c.Y); w.WriteEndArray(); }
            w.WriteEndArray();
            if (e is IFace fc && fc.CornerZs.Count > 0)
            {
                w.WriteStartArray("cornerZs");
                foreach (var z in fc.CornerZs) NumValue(w, z);
                w.WriteEndArray();
                w.WriteBoolean("cornerZIsZero", fc.CornerZIsZero);
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}
