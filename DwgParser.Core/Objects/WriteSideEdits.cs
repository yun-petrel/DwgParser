namespace DwgParser.Core.Objects;

internal static class WriteSideEdits
{
    public static bool ObjectStreamEdited { get; private set; }

    public static readonly HashSet<ulong> DroppedHandles = new();

    public static bool IsDropped(ulong handle)
        => DroppedHandles.Contains(handle) || OracleFrameBypass.DropHandles.Contains(handle);

    public static IEnumerable<ulong> AllDropped()
    {
        foreach (var h in DroppedHandles) yield return h;
        foreach (var h in OracleFrameBypass.DropHandles)
            if (!DroppedHandles.Contains(h)) yield return h;
    }

    public static void MarkDropped(ulong handle) => DroppedHandles.Add(handle);

    public static bool HasPendingChanges
        => ObjectStreamEdited || OracleFrameBypass.HasPendingDrops
           || DroppedHandles.Count > 0 || AttributeEdits.Count > 0;


    public sealed record StringFieldEdit(int FieldIndex, string Value);

    private static readonly Dictionary<ulong, List<StringFieldEdit>> AttributeEdits = new();

    public static bool HasAttributeEdits => AttributeEdits.Count > 0;

    public static void MarkAttributeEdited(ulong handle, IReadOnlyList<StringFieldEdit> edits)
        => AttributeEdits[handle] = new List<StringFieldEdit>(edits);

    public static IReadOnlyList<StringFieldEdit> AttributeEditsFor(ulong handle)
        => AttributeEdits.TryGetValue(handle, out var e) ? e : Array.Empty<StringFieldEdit>();

    public static void NoteFieldEmitted(ulong handle, int fieldIndex)
    {
        if (!AttributeEdits.TryGetValue(handle, out var list)) return;
        list.RemoveAll(e => e.FieldIndex == fieldIndex);
        if (list.Count == 0) AttributeEdits.Remove(handle);
    }

    public static void ConsumeAttributeEdit(ulong handle) => AttributeEdits.Remove(handle);

    public static IReadOnlyCollection<ulong> UnappliedAttributeEdits => AttributeEdits.Keys;

    public static void ClearAttributeEdits() => AttributeEdits.Clear();

    public static void MarkObjectStreamEdited() => ObjectStreamEdited = true;

    public static DwgObjectReader.WalkResult? EditedWalk { get; private set; }

    public static void SetEditedWalk(DwgObjectReader.WalkResult walk) => EditedWalk = walk;

    public static void Reset()
    {
        ObjectStreamEdited = false;
        EditedWalk = null;
        AttributeEdits.Clear();
        DroppedHandles.Clear();
    }
}
