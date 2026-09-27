using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static class OracleFrameBypass
{
    public static DwgVersion OracleVersion;

    public static readonly Dictionary<ulong, byte[]> Frames = new();

    public static readonly HashSet<ulong> EnabledOnly = new();

    public static bool HasPendingDrops => DropHandles.Count > 0;

    public static readonly HashSet<ulong> DropHandles = new();

    public static readonly List<ulong> LastBypassed = new();

    public static int Hits, Misses;

    private static bool _envTried;

    public static void Reset()
    {
        Frames.Clear(); EnabledOnly.Clear(); DropHandles.Clear(); LastBypassed.Clear();
        OracleVersion = 0; Hits = Misses = 0; _envTried = false;
    }

    public static bool TryGetBypassFrame(DwgVersion target, ulong handle, out byte[] frame)
    {
        frame = Array.Empty<byte>();
        if (OracleVersion == 0 && Frames.Count == 0 && !_envTried) TryLoadFromEnv();
        if (Frames.Count == 0 || OracleVersion != target) return false;
        if (EnabledOnly.Count > 0 && !EnabledOnly.Contains(handle)) { Misses++; return false; }
        if (!Frames.TryGetValue(handle, out var f) || f is null) { Misses++; return false; }
        Hits++;
        frame = f;
        return true;
    }

    public static int LoadFromOracle(R2004File oracle)
    {
        bool r2010 = oracle.Version.IsR2010Plus();
        if (!r2010 && oracle.Version != DwgVersion.AC1021 && oracle.Version != DwgVersion.AC1018)
            throw new NotSupportedException($"oracle 帧旁路仅支持 AC1018/AC1021/R2010+ 目标布局，收到 {oracle.VersionName}");
        var objects = oracle.DecompressSection("AcDb:AcDbObjects")
            ?? throw new InvalidDataException("oracle 无 AcDb:AcDbObjects 节");
        var handles = oracle.DecompressSection("AcDb:Handles")
            ?? throw new InvalidDataException("oracle 无 AcDb:Handles 节");
        var offs = new List<int>();
        var hs = new List<ulong>();
        if (!DwgObjectReader.TestWalk(handles, offs, hs))
            throw new InvalidDataException("oracle 句柄映射解析失败");

        Frames.Clear();
        int loaded = 0;
        for (int i = 0; i < offs.Count && i < hs.Count; i++)
        {
            int start = offs[i];
            if (start <= 0 || start + 4 > objects.Length) continue;
            int p = start;
            uint ms = ReadMs(objects, ref p);
            int end;
            if (r2010)
            {
                long umc = ReadUmc(objects, p, out int bodyStart);
                end = bodyStart + (int)ms;
                if (umc < 0) continue;
            }
            else
            {
                end = p + (int)ms;
            }
            if (ms == 0 || end < p + 2 || end + 2 > objects.Length) continue;
            var frame = new byte[end + 2 - start];
            Array.Copy(objects, start, frame, 0, frame.Length);
            Frames[hs[i]] = frame;
            loaded++;
        }
        OracleVersion = oracle.Version;
        EnabledOnly.Clear();
        LastBypassed.Clear();
        Hits = Misses = 0;
        return loaded;
    }

    public static int LoadFromOracle(R2000File oracle)
    {
        var raw = oracle.Raw;
        var hs = oracle.Handles;
        var offs = oracle.ObjectOffsets;
        if (hs.Count == 0 || offs.Count != hs.Count)
            throw new InvalidDataException("R2000 oracle 无对象映射");
        var order = Enumerable.Range(0, hs.Count).OrderBy(i => offs[i]).ToList();
        int streamEnd = raw.Length;
        foreach (var r in oracle.Records)
            if (r.Address > offs[order[^1]] && r.Address < streamEnd) streamEnd = (int)r.Address;

        Frames.Clear();
        int loaded = 0;
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k], start = offs[i];
            if (start <= 0 || start + 4 > raw.Length || hs[i] == 0) continue;
            int end = k + 1 < order.Count ? offs[order[k + 1]] : streamEnd;
            if (end <= start || end > raw.Length) continue;
            var frame = new byte[end - start];
            Array.Copy(raw, start, frame, 0, frame.Length);
            Frames[hs[i]] = frame;
            loaded++;
        }
        OracleVersion = oracle.Version;
        EnabledOnly.Clear();
        LastBypassed.Clear();
        Hits = Misses = 0;
        return loaded;
    }


    public static void SaveDump(string path, IReadOnlyDictionary<ushort, short> typeCodes)
    {
        using var bw = new BinaryWriter(File.Create(path));
        bw.Write((byte)0x4F); bw.Write((byte)0x52); bw.Write((byte)0x46); bw.Write((byte)0x52);
        bw.Write((ushort)1);
        bw.Write((ushort)(ushort)OracleVersion);
        bw.Write(Frames.Count);
        foreach (var (h, frame) in Frames)
        {
            bw.Write(h);
            bw.Write(typeCodes.TryGetValue((ushort)h, out short t) ? t : (short)0);
            bw.Write(frame.Length);
            bw.Write(frame);
        }
    }

    public static int LoadDump(string path)
    {
        using var br = new BinaryReader(File.OpenRead(path));
        if (br.ReadBytes(4) is not { Length: 4 } magic || magic[0] != 0x4F || magic[1] != 0x52
            || magic[2] != 0x46 || magic[3] != 0x52)
            throw new InvalidDataException($"非 ORFR 转储: {path}");
        br.ReadUInt16();
        OracleVersion = (DwgVersion)br.ReadUInt16();
        int n = br.ReadInt32();
        Frames.Clear();
        for (int i = 0; i < n; i++)
        {
            ulong h = br.ReadUInt64();
            br.ReadInt16();
            int len = br.ReadInt32();
            Frames[h] = br.ReadBytes(len);
        }
        EnabledOnly.Clear();
        LastBypassed.Clear();
        Hits = Misses = 0;
        return Frames.Count;
    }

    private static void TryLoadFromEnv()
    {
        _envTried = true;
        var path = System.Environment.GetEnvironmentVariable("DWG_ORACLE_FRAMES");
        if (string.IsNullOrEmpty(path)) return;
        LoadDump(path);
        var only = System.Environment.GetEnvironmentVariable("DWG_ORACLE_ONLY");
        if (!string.IsNullOrEmpty(only))
        {
            foreach (var tok in only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (ulong.TryParse(tok, System.Globalization.NumberStyles.HexNumber, null, out ulong h))
                    EnabledOnly.Add(h);
        }
    }


    private static uint ReadMs(byte[] data, ref int pos)
    {
        uint result = 0;
        for (int shift = 0; shift <= 15; shift += 15)
        {
            if (pos + 2 > data.Length) return 0;
            ushort w = (ushort)(data[pos] | (data[pos + 1] << 8));
            pos += 2;
            if ((w & 0x8000) == 0) return result | ((uint)w << shift);
            result |= (uint)(w & 0x7FFF) << shift;
        }
        return result;
    }

    private static long ReadUmc(byte[] data, int pos, out int newPos)
    {
        ulong result = 0;
        for (int shift = 0; shift <= 49; shift += 7)
        {
            if (pos >= data.Length) { newPos = -1; return -1; }
            byte b = data[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) { newPos = pos; return (long)result; }
        }
        newPos = -1;
        return -1;
    }
}
