using System.Security.Cryptography;
using System.Text;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public static class DwgEditor
{
    public static EditReport Apply(IDwgFile file, IReadOnlyList<EditOp> ops, string outPath,
        DwgVersion? target = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(ops);
        var walk = file.GetObjects();
        int entitiesBefore = walk.Entities.Count(e => !e.IsErased);
        int objectsBefore = walk.ObjectsSeen;

        var outcomes = new List<OpOutcome>(ops.Count);
        var dropped = new List<ulong>();
        var expected = new Dictionary<ulong, string>();
        var expectedTag = new Dictionary<ulong, string>();
        var expectedGeom = new Dictionary<ulong, (double X, double Y, double H)>();
        DwgVersion tgt = target ?? file.Version;
        try
        {
            for (int i = 0; i < ops.Count; i++)
            {
                string? why;
                OpStatus st;
                switch (ops[i])
                {
                    case DeleteOp d:
                        why = RegisterDelete(walk, d.Handle, dropped);
                        st = why is null ? OpStatus.Applied : OpStatus.Rejected;
                        Add(outcomes, i, "delete", d.Handle, st, why);
                        break;
                    case SetTextOp s:
                        why = RegisterText(walk, s.Handle, s.Value, expected);
                        st = why is null ? OpStatus.Applied : OpStatus.Rejected;
                        Add(outcomes, i, "setText", s.Handle, st, why);
                        break;
                    case SetAttribOp a:
                        why = RegisterAttrib(file, walk, a, expected, expectedTag);
                        st = why is null ? OpStatus.Applied : OpStatus.Rejected;
                        Add(outcomes, i, "setAttrib", a.Handle, st, why);
                        break;
                    case AddMTextOp m:
                        why = RegisterAddMText(file, tgt, walk, m, expected, expectedGeom,
                            out ulong newHandle);
                        st = why is null ? OpStatus.Applied : OpStatus.Rejected;
                        Add(outcomes, i, "addMText", newHandle, st, why);
                        break;
                    default:
                        Add(outcomes, i, Name(ops[i]), ops[i].Target ?? 0, OpStatus.Rejected,
                            "射程外：该算子本轮未实证");
                        break;
                }
            }

            var touched = new HashSet<ulong>(dropped);
            foreach (var k in expected.Keys) touched.Add(k);
            var coverage = new ReferenceCoverage(walk.ObjectsDecoded,
                walk.ObjectsSeen - walk.ObjectsDecoded,
                walk.ObjectsDecoded == walk.ObjectsSeen);
            string fpBefore = FingerprintUntouched(walk, touched);

            if (outcomes.Any(o => o.Status == OpStatus.Rejected))
                return Report(file, outPath, 0, false, entitiesBefore, objectsBefore,
                    entitiesBefore, objectsBefore, 0,
                    outcomes.Select(o => o.Status == OpStatus.Rejected ? o : o with
                    {
                        Status = OpStatus.Rejected,
                        Verified = false,
                        Reason = "同单内有他枚被拒，整体不落盘。"
                    }).ToList(), "", "", coverage);

            string tmpPath = outPath + ".tmp";
            WriteSideEdits.MarkObjectStreamEdited();
            WriteSideEdits.SetEditedWalk(walk);
            DwgConverter.Save(file, tgt, tmpPath);

            var rw = DwgConverter.Load(tmpPath).GetObjects();
            bool ok = rw.Errors == 0;
            var unclaimed = new HashSet<ulong>(WriteSideEdits.UnappliedAttributeEdits);

            var verified = new List<OpOutcome>(outcomes.Count);
            foreach (var o in outcomes)
            {
                string? miss = Verify(rw, o, expected, expectedTag, expectedGeom, unclaimed);
                ok &= miss is null;
                verified.Add(o with
                {
                    Verified = miss is null,
                    Status = miss is null ? o.Status : OpStatus.Unapplied,
                    Reason = miss
                });
            }

            long productBytes = new FileInfo(tmpPath).Length;
            if (ok) File.Move(tmpPath, outPath, overwrite: true);
            else File.Delete(tmpPath);

            return Report(file, outPath, ok ? productBytes : 0, ok, entitiesBefore, objectsBefore,
                rw.Entities.Count(e => !e.IsErased), rw.ObjectsSeen, rw.Errors, verified, fpBefore,
                FingerprintUntouched(rw, touched), coverage);
        }
        finally
        {
            WriteSideEdits.Reset();
        }
    }

    public static InspectResult Inspect(IDwgFile file, string? kindOnly = null,
        string? contains = null, int limit = 0)
    {
        ArgumentNullException.ThrowIfNull(file);
        var walk = file.GetObjects();
        var list = new List<EditCandidate>();
        foreach (var e in walk.Entities.Where(x => !x.IsErased).OrderBy(x => x.Handle))
        {
            if (kindOnly is not null && KindName(e.Kind) != kindOnly) continue;
            if (contains is not null && !e.TextValue.Contains(contains,
                    StringComparison.OrdinalIgnoreCase)) continue;
            var ops = new List<string>();
            if (TextRejection(walk, e.Handle, e.TextValue + " ") is null) ops.Add("setText");
            if (AttribRejection(walk, e.Handle) is null) ops.Add("setAttrib");
            if (DeleteRejection(walk, e.Handle) is null) ops.Add("delete");
            if (ops.Count == 0) continue;
            list.Add(new EditCandidate(e.Handle, KindName(e.Kind), e.TextValue, ops));
            if (limit > 0 && list.Count >= limit) break;
        }
        return new InspectResult(file.VersionName, walk.ObjectsDecoded,
            walk.ObjectsSeen - walk.ObjectsDecoded, list);
    }

    private static void Add(List<OpOutcome> list, int index, string name, ulong handle,
        OpStatus status, string? reason)
        => list.Add(new OpOutcome(index, name, handle, status, false, reason));

    private static string Name(EditOp op) => op switch
    {
        AddMTextOp _ => "addMText",
        _ => op.GetType().Name,
    };

    internal static string KindName(DwgEntityType kind) => kind.ToString();


    private static string? DeleteRejection(DwgObjectReader.WalkResult walk, ulong handle)
    {
        var ent = walk.Entities.Find(e => e.Handle == handle && !e.IsErased);
        if (ent is null) return $"射程外：h={handle:X} 不是本图里的存活实体。";
        if (ent.Kind is DwgEntityType.BlockHeader or DwgEntityType.Block or DwgEntityType.EndBlk
            or DwgEntityType.SeqEnd or DwgEntityType.Insert or DwgEntityType.Attrib
            or DwgEntityType.AttDef)
            return $"射程外：{ent.Kind} 是结构宿主/属性族，摘帧不同步其计数与序列 = 制造悬挂引用。";
        if (!walk.Records.Any(r => r.Handle == handle))
            return $"射程外：h={handle:X} 在对象流里没有对应记录（写侧无搬运面）。";

        foreach (var r in walk.Records)
        {
            if (r.Handle == handle || r.Handle == ent.OwnerHandle) continue;
            if (r.BlockOwnedHandles?.Contains(handle) == true)
                return $"射程外：h={r.Handle:X}（非本枚宿主）的 owned 向量指向它。";
            if (r.ReactorHandles.Any(hr => hr.Resolve(r.Handle) == handle))
                return $"射程外：h={r.Handle:X} 的反应器指向它。";
        }
        return null;
    }

    private static string? TextRejection(DwgObjectReader.WalkResult walk, ulong handle,
        string value)
    {
        var ent = walk.Entities.Find(e => e.Handle == handle && !e.IsErased);
        if (ent is null) return $"射程外：h={handle:X} 不是本图里的存活实体。";
        if (ent.Kind is not (DwgEntityType.MText or DwgEntityType.Text))
            return $"射程外：setText 只接 TEXT/MTEXT，本枚是 {ent.Kind}。";
        var rec = walk.Records.Find(r => r.Handle == handle);
        if (rec is null) return $"射程外：h={handle:X} 在对象流里没有对应记录（写侧无搬运面）。";
        if (rec.StringFields.Count == 0) return $"射程外：h={handle:X} 未捕获任何串字段。";
        if (rec.StringFields[0].Value != ent.TextValue)
            return $"射程外：h={handle:X} 的串捕获[0] 与实体正文不一致 ⇒ 序位不可信，未改。";
        return value is null ? "射程外：新值缺失。" : null;
    }

    private static string? AttribRejection(DwgObjectReader.WalkResult walk, ulong handle)
    {
        var ent = walk.Entities.Find(e => e.Handle == handle && !e.IsErased);
        if (ent is null || ent.Kind != DwgEntityType.Attrib)
            return $"射程外：h={handle:X} 不是 ATTRIB。";
        var rec = walk.Records.Find(r => r.Handle == handle);
        if (rec is null || rec.StringFields.Count < 2)
            return $"射程外：h={handle:X} 的串捕获不足两枚（正文 + 标记）。";
        if (rec.StringFields[0].Value != ent.TextValue || rec.StringFields[1].Value != ent.Tag)
            return $"射程外：h={handle:X} 的串捕获与实体槽不一致 ⇒ 序位不可信，未改。";
        return null;
    }


    private static string? RegisterDelete(DwgObjectReader.WalkResult walk, ulong handle,
        List<ulong> dropped)
    {
        string? why = DeleteRejection(walk, handle);
        if (why is null)
        {
            WriteSideEdits.MarkDropped(handle);
            dropped.Add(handle);
        }
        return why;
    }

    private static string? RegisterText(DwgObjectReader.WalkResult walk, ulong handle,
        string value, Dictionary<ulong, string> expected)
    {
        string? why = TextRejection(walk, handle, value);
        if (why is null)
        {
            var rec = walk.Records.First(r => r.Handle == handle);
            var ent = walk.Entities.First(e => e.Handle == handle);
            rec.StringFields[0] = rec.StringFields[0] with { Value = value };
            ent.TextValue = value;
            WriteSideEdits.MarkAttributeEdited(handle,
                new[] { new WriteSideEdits.StringFieldEdit(0, value) });
            expected[handle] = value;
        }
        return why;
    }

    private static string? RegisterAttrib(IDwgFile file, DwgObjectReader.WalkResult walk,
        SetAttribOp a, Dictionary<ulong, string> expected, Dictionary<ulong, string> expectedTag)
    {
        string? why = DwgAttributeEditor.TryEdit(file, a.Handle, a.Text, a.Tag);
        if (why is null)
        {
            var e = walk.Entities.First(x => x.Handle == a.Handle);
            if (a.Text is not null) expected[a.Handle] = a.Text;
            else expected[a.Handle] = e.TextValue;
            if (a.Tag is not null) expectedTag[a.Handle] = a.Tag;
        }
        return why;
    }

    private static string? RegisterAddMText(IDwgFile file, DwgVersion target,
        DwgObjectReader.WalkResult walk, AddMTextOp m, Dictionary<ulong, string> expected,
        Dictionary<ulong, (double X, double Y, double H)> expectedGeom, out ulong newHandle)
    {
        newHandle = 0;
        if (m.Value.Length == 0) return "射程外：addMText 的正文为空。";
        if (target == DwgVersion.AC1015 && file.Version != DwgVersion.AC1015)
            return "射程外：AC1015 目标的合成帧只在「源同为 AC1015」的链首支上实证过（异档源未验）。";

        if (m.Layer is not null)
            return "射程外：按名指定图层本轮未实装（新帧现借图内既有层，见 PickStyleAndLayer）。";

        newHandle = SyntheticDrawingBuilder.AppendMText(walk, m.X, m.Y, m.Value, m.Height,
            FileHeader.LegacyEncodingOf(file.RawFile ?? Array.Empty<byte>()), target);
        if (newHandle == 0)
            return "射程外：R2000 宿主块头链首那枚引用装不下新句柄（同宽改写落空，不交出一枚无人认领的帧）。";
        expected[newHandle] = m.Value;
        expectedGeom[newHandle] = (m.X, m.Y, m.Height);
        return null;
    }


    private static string? Verify(DwgObjectReader.WalkResult rw, OpOutcome o,
        IReadOnlyDictionary<ulong, string> expected,
        IReadOnlyDictionary<ulong, string> expectedTag,
        IReadOnlyDictionary<ulong, (double X, double Y, double H)> expectedGeom,
        HashSet<ulong> unclaimed)
    {
        if (o.Status != OpStatus.Applied) return o.Reason;
        if (o.OpName == "delete")
            return rw.Entities.Any(e => e.Handle == o.Handle)
                ? $"自证不合：产物回读里 h={o.Handle:X} 仍在场（删除未落盘）。"
                : null;
        if (o.OpName is not ("setText" or "setAttrib" or "addMText")) return null;

        var e = rw.Entities.Find(x => x.Handle == o.Handle);
        if (e is null) return $"自证不合：产物回读里 h={o.Handle:X} 不见了。";
        var why = new List<string>();
        if (expected.TryGetValue(o.Handle, out var want) && e.TextValue != want)
            why.Add($"正文与登记值不等（登记 {Cut(want)} / 产物 {Cut(e.TextValue)}）");
        if (expectedTag.TryGetValue(o.Handle, out var wtag) && e.Tag != wtag)
            why.Add($"标记与登记值不等（登记 {Cut(wtag)} / 产物 {Cut(e.Tag)}）");
        if (expectedGeom.TryGetValue(o.Handle, out var g)
            && (Math.Abs(e.Start.X - g.X) > 1e-6 || Math.Abs(e.Start.Y - g.Y) > 1e-6
                || Math.Abs(e.Height - g.H) > 1e-6))
            why.Add($"插点/字高不是给定值（给定 ({g.X},{g.Y}) h={g.H} / 产物 ({e.Start.X},{e.Start.Y}) h={e.Height}）");
        if (why.Count == 0) return null;
        if (unclaimed.Contains(o.Handle))
            why.Add("写侧未领用该枚 ⇒ 这一档的值级改写闸未放行（闸与实证见 DwgObjectStreamWriter.AttrEdit 文件头）");
        return $"自证不合：产物回读 h={o.Handle:X} " + string.Join("；", why) + "。";
    }

    private static string Cut(string s) => s.Length <= 24 ? s : "…" + s[(^12)..];

    private static string FingerprintUntouched(DwgObjectReader.WalkResult walk,
        HashSet<ulong> touched)
    {
        var sb = new StringBuilder();
        foreach (var e in walk.Entities.Where(e => !e.IsErased && !touched.Contains(e.Handle))
                     .OrderBy(e => e.Handle))
            sb.Append(e.Handle.ToString("X")).Append(':').Append((int)e.Kind).Append(':')
              .Append(e.LayerHandle.ToString("X")).Append(':').Append(e.ColorIndex).Append(':')
              .Append(e.TextValue).Append(';');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static EditReport Report(IDwgFile file, string outPath, long productBytes, bool written,
        int entitiesBefore, int objectsBefore, int entitiesAfter, int objectsAfter, int walkErrors,
        IReadOnlyList<OpOutcome> outcomes, string fpBefore, string fpAfter,
        ReferenceCoverage coverage)
        => new(file.VersionName, file.FileSize, outPath, productBytes, written,
            new EditCounts(entitiesBefore, entitiesAfter, objectsBefore, objectsAfter, walkErrors),
            fpBefore, fpAfter, coverage, outcomes);
}
