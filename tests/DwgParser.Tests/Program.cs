using System.Reflection;
using DwgParser.Core;
using DwgParser.Core.Objects;
using DwgParser.Core.Format;
using DwgParser.Core.Render;

int _fail = 0, _total = 0;

void Ok(bool cond, string name)
{
    _total++;
    if (!cond) { _fail++; Console.WriteLine($"  [FAIL] {name}"); }
}

Console.WriteLine("== codec 注册表全量 ==");
var versions = Enum.GetValues<DwgVersion>();
short[] entityCodes =
[
    DwgObjectReader.TypeText, DwgObjectReader.TypeAttrib, DwgObjectReader.TypeAttDef,
    DwgObjectReader.TypeBlockEntity, DwgObjectReader.TypeEndBlk, DwgObjectReader.TypeSeqEnd,
    DwgObjectReader.TypeInsert, DwgObjectReader.TypeMInsert, DwgObjectReader.TypeVertex2D,
    DwgObjectReader.TypePolyline2D, DwgObjectReader.TypeArc, DwgObjectReader.TypeCircle,
    DwgObjectReader.TypeLine, DwgObjectReader.TypePoint, DwgObjectReader.Type3dFace,
    DwgObjectReader.TypeSolid, DwgObjectReader.TypeTrace, DwgObjectReader.TypeMText,
    DwgObjectReader.TypeDimOrdinate, DwgObjectReader.TypeDimLinear, DwgObjectReader.TypeDimAligned,
    DwgObjectReader.TypeDimAng3pt, DwgObjectReader.TypeDimAng2ln, DwgObjectReader.TypeDimRadius,
    DwgObjectReader.TypeDimDiameter, DwgObjectReader.TypeEllipse, DwgObjectReader.TypeSpline,
    DwgObjectReader.TypeRegion, DwgObjectReader.TypeSolid3d, DwgObjectReader.TypeBody,
    DwgObjectReader.TypeRay, DwgObjectReader.TypeXLine, DwgObjectReader.TypeLeader,
    DwgObjectReader.TypeTolerance, DwgObjectReader.TypeMline, DwgObjectReader.TypeViewport,
    DwgObjectReader.TypeLwPolyline, DwgObjectReader.TypeHatch, DwgObjectReader.TypeTable,
    DwgObjectReader.TypeMLeader,
];
foreach (var code in entityCodes)
    foreach (var v in versions)
        Ok(EntityCodecRegistry.Default.Resolve(code, v) is not null,
           $"0x{code:X} @ {v} 未注册 codec");

Console.WriteLine("== 渲染注册表全量表态 ==");
DwgEntityType[] rasterStructural =
    [DwgEntityType.Unknown, DwgEntityType.Vertex2D, DwgEntityType.SeqEnd,
     DwgEntityType.BlockHeader, DwgEntityType.EndBlk, DwgEntityType.Block, DwgEntityType.Insert];
DwgEntityType[] svgStructural =
    [.. rasterStructural, DwgEntityType.Point, DwgEntityType.Ellipse, DwgEntityType.Hatch,
     DwgEntityType.Table, DwgEntityType.MLeader, DwgEntityType.PointSequence];
foreach (DwgEntityType k in Enum.GetValues<DwgEntityType>())
{
    if (svgStructural.Contains(k))
        Ok(EntityRenderRegistry.ForSvg(k) is null, $"ForSvg({k}) 应为结构性 null");
    else
        Ok(EntityRenderRegistry.ForSvg(k) is not null, $"ForSvg({k}) 缺 renderer");
    if (rasterStructural.Contains(k))
        Ok(EntityRenderRegistry.For(k) is null, $"For({k}) 应为结构性 null");
    else
        Ok(EntityRenderRegistry.For(k) is not null, $"For({k}) 缺 renderer");
}

Console.WriteLine("== 实体集合语义 ==");
var owned = new EntityCollection();
var e1 = new DwgEntity { Handle = 0x11 };
var e2 = new DwgEntity { Handle = 0x22, OwnerHandle = 0x99 };
owned.Add(e1);
owned.Add(e2);
Ok(owned.Count == 2, "Add 后 Count");
Ok(owned.Find(0x11) == e1, "Find 命中");
Ok(owned.Find(0x33) is null, "Find 未命中");
var dup = new DwgEntity { Handle = 0x11 };
owned.Add(dup);
Ok(owned.Find(0x11) == e1 && owned.Count == 3, "句柄重复先登优先");
Ok(owned.Remove(0x11) && owned.Find(0x11) is null, "Remove 返回真并移除");
Ok(!owned.Remove(0x11), "重复 Remove 返回假");
Ok(owned.OfOwner(0x99).SequenceEqual([e2]), "OfOwner 分组");
owned.Invalidate();
Ok(owned.Find(0x22) is null && owned.Count == 2, "Invalidate 清派生索引");

var src = new List<IEntity>();
var attached = EntityCollection.Attach(src);
var a1 = new DwgEntity { Handle = 0xA1, OwnerHandle = 0x7 };
src.Add(a1);
Ok(attached.Find(0xA1) == a1, "附着: 源 Add 后 Find 可见");
var a2 = new DwgEntity { Handle = 0xA2, OwnerHandle = 0x7 };
src.Add(a2);
Ok(attached.Find(0xA2) == a2, "附着: 二次 Add 后脏检重建");
Ok(attached.Count == 2 && attached.Items.Count == 2, "附着 Items 即源列表");
src.RemoveAt(0);
Ok(attached.Find(0xA1) is null && attached.Count == 1, "附着: 源 Remove 后索引跟进");
Ok(attached.OfOwner(0x7).SequenceEqual([a2]), "附着 OfOwner 活分组");
try { attached.Add(new DwgEntity { Handle = 1 }); Ok(false, "附着 Add 应显式炸"); }
catch (InvalidOperationException) { Ok(true, "附着 Add 显式炸"); }
try { attached.Remove(0xA2); Ok(false, "附着 Remove 应显式炸"); }
catch (InvalidOperationException) { Ok(true, "附着 Remove 显式炸"); }

Console.WriteLine("== 族模型默认值（历史初始化器锁定） ==");
var de = new DwgEntity { Handle = 1 };
Ok(de.WidthFactor == 1.0, "WidthFactor=1.0");
Ok(de.ScaleX == 1.0 && de.ScaleY == 1.0 && de.ScaleZ == 1.0, "Scale*=1.0");
Ok(de.LineSpacingFactor == 1.0, "LineSpacingFactor=1.0");
Ok(de.AttIsLockedInBlock == -1 && de.AttKeepDuplicateRecords == -1 && de.AttMTextType == -1, "Att*=-1");
Ok(de.ColorIndex == 256 && de.ExtrusionZ == 1.0, "公共字段默认");
Ok(de.TextValue == "" && de.DimSubtype == "" && de.Name == "", "字符串默认空");
Ok(de.Points.Count == 0 && de.Bulges.Count == 0, "列表惰性空");
de.Points.Add(new Vec2(1, 2));
Ok(new DwgEntity { Handle = 2 }.Points.Count == 0, "实例间列表不共享");

Console.WriteLine("== 版本分派旗标（codec Create 抽查） ==");
var mt15 = EntityCodecRegistry.Default.Resolve(DwgObjectReader.TypeMText, DwgVersion.AC1015)!.Create(1, DwgObjectReader.TypeMText, DwgVersion.AC1015);
var mt32 = EntityCodecRegistry.Default.Resolve(DwgObjectReader.TypeMText, DwgVersion.AC1032)!.Create(2, DwgObjectReader.TypeMText, DwgVersion.AC1032);
Ok(mt15 is MTextAcR2000, "MText@AC1015 → MTextAcR2000");
Ok(mt32 is MTextAcR2018Plus, "MText@AC1032 → MTextAcR2018Plus");
var sp27 = EntityCodecRegistry.Default.Resolve(DwgObjectReader.TypeSpline, DwgVersion.AC1027)!.Create(3, DwgObjectReader.TypeSpline, DwgVersion.AC1027);
Ok(sp27 is SplineAcR2013Plus, "Spline@AC1027 → SplineAcR2013Plus");
var face = EntityCodecRegistry.Default.Resolve(DwgObjectReader.Type3dFace, DwgVersion.AC1015)!.Create(4, DwgObjectReader.Type3dFace, DwgVersion.AC1015);
var solid = EntityCodecRegistry.Default.Resolve(DwgObjectReader.TypeSolid, DwgVersion.AC1015)!.Create(5, DwgObjectReader.TypeSolid, DwgVersion.AC1015);
Ok(face is FaceEntity, "3dFace 分型正确");
Ok(face.Kind == DwgEntityType.Unknown, "约定: Kind 由 ReadBody/ReadGeometry 设置（创建态 Unknown）");
Ok(solid is SolidEntity, "Solid 分型正确");
var dim = EntityCodecRegistry.Default.Resolve(DwgObjectReader.TypeDimRadius, DwgVersion.AC1015)!.Create(6, DwgObjectReader.TypeDimRadius, DwgVersion.AC1015);
Ok(dim is DimensionRadius, "DIMENSION 子码 0x19 → DimensionRadius");

Console.WriteLine("== 编辑契约拒绝判据 ==");
DwgObjectRecord Rec(ulong h) { var r = new DwgObjectRecord(); r.Handle = h; return r; }
var walk = new DwgObjectReader.WalkResult();
var mtext = new DwgEntity { Handle = 0x101, Kind = DwgEntityType.MText, TextValue = "原文" };
var rec1 = Rec(0x101);
rec1.StringFields.Add(new DwgObjectRecord.StringField(0, 8, "原文"));
walk.Entities.Add(mtext);
walk.Records.Add(rec1);
Ok(DwgEditor.TextRejection(walk, 0x101, "新值") is null, "setText: MText+串捕获一致 → 放行");
Ok(DwgEditor.DeleteRejection(walk, 0x101) is null, "delete: 有记录无引用 → 放行");
Ok(DwgEditor.DeleteRejection(walk, 0x102) != null, "delete: 射程外句柄 → 拒");
Ok(DwgEditor.TextRejection(walk, 0x102, "x") != null, "setText: 射程外句柄 → 拒");
var line103 = new DwgEntity { Handle = 0x103, Kind = DwgEntityType.Line };
walk.Entities.Add(line103);
Ok(DwgEditor.TextRejection(walk, 0x103, "x") != null, "setText: 非 TEXT/MTEXT 族 → 拒");
var ins = new DwgEntity { Handle = 0x104, Kind = DwgEntityType.Insert };
walk.Entities.Add(ins);
walk.Records.Add(Rec(0x104));
Ok(DwgEditor.DeleteRejection(walk, 0x104) != null, "delete: 结构宿主（INSERT）→ 拒");
var erased = new DwgEntity { Handle = 0x105, Kind = DwgEntityType.MText, IsErased = true };
walk.Entities.Add(erased);
Ok(DwgEditor.DeleteRejection(walk, 0x105) != null, "delete: 已擦除实体 → 拒");
var m2 = new DwgEntity { Handle = 0x106, Kind = DwgEntityType.MText, TextValue = "正文A" };
var rec2 = Rec(0x106);
rec2.StringFields.Add(new DwgObjectRecord.StringField(0, 8, "正文B"));
walk.Entities.Add(m2);
walk.Records.Add(rec2);
Ok(DwgEditor.TextRejection(walk, 0x106, "x") != null, "setText: 串捕获与正文不一致（序位不可信）→ 拒");
var m3 = new DwgEntity { Handle = 0x107, Kind = DwgEntityType.MText, TextValue = "无记录" };
walk.Entities.Add(m3);
Ok(DwgEditor.TextRejection(walk, 0x107, "x") != null, "setText: 无对应记录 → 拒");
var attr = new DwgEntity { Handle = 0x108, Kind = DwgEntityType.Attrib, TextValue = "值1", Tag = "TAG1" };
var rec3 = Rec(0x108);
rec3.StringFields.Add(new DwgObjectRecord.StringField(0, 8, "值1"));
rec3.StringFields.Add(new DwgObjectRecord.StringField(8, 8, "TAG1"));
walk.Entities.Add(attr);
walk.Records.Add(rec3);
Ok(DwgEditor.AttribRejection(walk, 0x108) is null, "setAttrib: 正文+标记两枚捕获一致 → 放行");
Ok(DwgEditor.AttribRejection(walk, 0x101) != null, "setAttrib: 非 ATTRIB → 拒");
var rec4 = Rec(0x109);
rec4.StringFields.Add(new DwgObjectRecord.StringField(0, 8, "只有一枚"));
var attr2 = new DwgEntity { Handle = 0x109, Kind = DwgEntityType.Attrib, TextValue = "只有一枚" };
walk.Entities.Add(attr2);
walk.Records.Add(rec4);
Ok(DwgEditor.AttribRejection(walk, 0x109) != null, "setAttrib: 串捕获不足两枚 → 拒");
var host = new DwgEntity { Handle = 0x110, Kind = DwgEntityType.MText, TextValue = "被指向", OwnerHandle = 0x111 };
walk.Entities.Add(host);
walk.Records.Add(Rec(0x110));
var recH = Rec(0x111);
recH.BlockOwnedHandles = new List<ulong> { 0x110UL };
walk.Entities.Add(new DwgEntity { Handle = 0x111, Kind = DwgEntityType.Line });
walk.Records.Add(recH);
var recX = Rec(0x112);
recX.BlockOwnedHandles = new List<ulong> { 0x110UL };
walk.Entities.Add(new DwgEntity { Handle = 0x112, Kind = DwgEntityType.Line });
walk.Records.Add(recX);
Ok(DwgEditor.DeleteRejection(walk, 0x110) != null, "delete: 被非宿主 owned 向量指向 → 拒");
Ok(DwgEditor.DeleteRejection(walk, 0x113) != null, "delete: 无记录实体 → 拒（对照宿主向量豁免）");

Console.WriteLine($"----\n{_total - _fail}/{_total} PASS");
return _fail == 0 ? 0 : 1;
