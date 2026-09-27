using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class RenderSettingsRecord : IRenderSettings
{
    internal const int MaxTextChars = 8_192;

    public const int BaseFieldCount = 6;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    private List<DwgObjectRecord.StringField>? _texts;

    protected RenderSettingsRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.RenderSettings ??= new RenderSettingsCapture();
        Cap.AttachIdentity(rec);
        Cap.SettingsKind = KindOf(rec.ClassName);
    }

    public RenderSettingsCapture Cap { get; }

    public abstract string Family { get; }

    protected abstract IReadOnlyList<(string Label, RenderSettingKind Kind)> TailFields { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string SettingsKind => Cap.SettingsKind;
    public long ClassVersion => Cap.ClassVersion;
    public string Name => Cap.Name;
    public bool FogEnabled => Cap.FogEnabled;
    public bool FogBackgroundEnabled => Cap.FogBackgroundEnabled;
    public bool BackfacesEnabled => Cap.BackfacesEnabled;
    public bool EnvironImageEnabled => Cap.EnvironImageEnabled;
    public string EnvironImageFilename => Cap.EnvironImageFilename;
    public string Description => Cap.Description;
    public long DisplayIndex => Cap.DisplayIndex;
    public IReadOnlyList<RenderSettingField> Fields => Cap.Fields;
    public bool Captured => Cap.Captured;

    protected virtual bool HasInlineRl => true;
    protected virtual bool HasXdicMissing => false;
    protected virtual bool StringsCentralized => false;

    internal static string KindOf(string? className)
    {
        var n = (className ?? "").ToUpperInvariant();
        if (n.Contains("MENTALRAY")) return "MENTALRAY";
        if (n.Contains("RAPIDRT")) return "RAPIDRT";
        return "";
    }

    internal virtual void Read(BitReader shared)
    {
        if (Cap.SettingsKind.Length == 0) { Fail("种类未知"); return; }
        _texts = StringsCentralized ? null : new List<DwgObjectRecord.StringField>();

        var br = new BitReader(shared.UnderlyingData(), 0) { UnicodeStrings = StringsCentralized };
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        long dataStart = (long)ObjStart * 8;
        uint bitsize = 0;
        long hs;
        if (HasInlineRl)
        {
            bitsize = br.ReadRL();
            hs = dataStart + bitsize;
        }
        else hs = Rec.HandleStreamStartBit;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail("帧界"); return; }

        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail("EED"); return; }
        if (br.ReadBL() is < 0 or > 100_000) { Fail("num_reactors"); return; }
        if (HasXdicMissing) br.ReadB();

        long target = hs;
        if (StringsCentralized)
        {
            var scratch = new DwgObjectRecord();
            DwgObjectReader.AttachStringStream(br, ObjStart, bitsize, Rec.SourceVersion, scratch);
            if (scratch.StringStreamStartBit <= 0) { Fail("无集中串区"); return; }
            target = scratch.StringStreamStartBit;
        }

        var tail = new List<RenderSettingField>(TailFields.Count);
        if (!ReadBase(br, tail) || !ReadTail(br, tail)) return;

        if (br.BitPosition != target) { Fail($"停点 {br.BitPosition - target}"); return; }

        Cap.Fields = tail;
        Cap.WalkEndBit = br.BitPosition;
        Cap.FailReason = "";
        Cap.Captured = true;
        if (_texts is { Count: > 0 }) Rec.StringFields.AddRange(_texts);
        _texts = null;
    }

    private bool ReadBase(BitReader br, List<RenderSettingField> tail)
    {
        long b = br.BitPosition;
        long version = br.ReadBL();
        if (version is < 0 or > 32) return Bail("class_version 越界");
        Cap.ClassVersion = version;
        Add(tail, "class_version", RenderSettingKind.Int32, version, 0, "", br.BitPosition - b);

        if (!ReadText(br, tail, "name", RequiredName, s => Cap.Name = s)) return false;
        b = br.BitPosition;
        Cap.FogEnabled = br.ReadB();
        Cap.FogBackgroundEnabled = br.ReadB();
        Cap.BackfacesEnabled = br.ReadB();
        Cap.EnvironImageEnabled = br.ReadB();
        Add(tail, "fog,fog_background,backfaces,environ_image(290×4)", RenderSettingKind.Flag,
            (Cap.FogEnabled ? 1 : 0) | (Cap.FogBackgroundEnabled ? 2 : 0)
                | (Cap.BackfacesEnabled ? 4 : 0) | (Cap.EnvironImageEnabled ? 8 : 0),
            0, "", br.BitPosition - b);
        if (!ReadText(br, tail, "environ_image_filename", s => s.Length == 0 || s.Length <= MaxTextChars,
                s => Cap.EnvironImageFilename = s)) return false;
        if (!ReadText(br, tail, "description", s => s.Length <= MaxTextChars, s => Cap.Description = s)) return false;

        b = br.BitPosition;
        long displayIndex = br.ReadBL();
        if (displayIndex < 0) return Bail("display_index 越界");
        Cap.DisplayIndex = displayIndex;
        Add(tail, "display_index", RenderSettingKind.Int32, displayIndex, 0, "", br.BitPosition - b);
        return true;
    }

    private bool ReadTail(BitReader br, List<RenderSettingField> tail)
    {
        foreach (var (label, kind) in TailFields)
        {
            long b = br.BitPosition;
            switch (kind)
            {
                case RenderSettingKind.Int32: {
                    long v = br.ReadBL();
                    Add(tail, label, kind, v, 0, "", br.BitPosition - b);
                    break;
                }
                case RenderSettingKind.Int16: {
                    long v = br.ReadBS();
                    Add(tail, label, kind, v, 0, "", br.BitPosition - b);
                    break;
                }
                case RenderSettingKind.Real: {
                    double v = br.ReadBD();
                    if (double.IsNaN(v) || Math.Abs(v) > 1e12) return Bail($"{label}=NaN/越界");
                    Add(tail, label, kind, 0, v, "", br.BitPosition - b);
                    break;
                }
                case RenderSettingKind.Flag: {
                    bool v = br.ReadB();
                    Add(tail, label, kind, v ? 1 : 0, 0, "", br.BitPosition - b);
                    break;
                }
                default: {
                    if (!ReadText(br, tail, label, s => s.Length <= MaxTextChars, _ => { })) return false;
                    break;
                }
            }
        }
        return true;
    }

    private bool ReadText(BitReader br, List<RenderSettingField> tail, string label,
        Func<string, bool> guard, Action<string> assign)
    {
        long b = br.BitPosition;
        string s = br.ReadTV();
        if (!guard(s)) return Bail($"{label} 越限");
        assign(s);
        Add(tail, label, RenderSettingKind.Text, 0, 0, s, (int)(br.BitPosition - b));
        _texts?.Add(new DwgObjectRecord.StringField(b, (int)(br.BitPosition - b), s));
        return true;
    }

    private static bool RequiredName(string s) => s.Length is > 0 and <= MaxTextChars;

    private static void Add(List<RenderSettingField> tail, string label, RenderSettingKind kind,
        long i, double r, string text, long bits)
        => tail.Add(new RenderSettingField(label, kind, i, r, text, (int)bits));

    private bool Bail(string why) { Fail(why); return false; }

    private void Fail(string why)
    {
        Cap.FailReason = why;
        Cap.WalkEndBit = -1;
    }
}
