using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;

namespace DwgParser.Cli;

public static partial class Program
{
    private const short WireVport = 0x41;
    private const short WireAppid = 0x43;
    private const short WireScale = 506;

    private static void FieldizeReport(string path, DwgDocument doc)
    {
        var objs = doc.GetObjects();
        Console.WriteLine($"FIELDIZE file={System.IO.Path.GetFileName(path)} ver={doc.Version} " +
            $"seen={objs.ObjectsSeen} decoded={objs.ObjectsDecoded} errors={objs.Errors}");

        var xrs = objs.Records.Where(r => r.TypeCode == 0x4F).ToList();
        int xrCap = xrs.Count(r => r.Xrecord is { Captured: true });
        Console.WriteLine($"xrecord total={xrs.Count} captured={xrCap} uncaptured={xrs.Count - xrCap}");
        var xrFail = new Dictionary<string, int>();
        foreach (var r in xrs)
        {
            if (r.Xrecord is { Captured: true }) continue;
            string reason = r.Xrecord?.FailReason ?? "(no capture)";
            string key = System.Text.RegularExpressions.Regex.Replace(reason, @"d+", "#");
            xrFail[key] = xrFail.GetValueOrDefault(key) + 1;
            string codes = string.Join(",", (r.Xrecord?.Items ?? new List<DwgParser.Core.Objects.Records.XrecordItem>())
                .TakeLast(14).Select(i => i.Code));
            Console.WriteLine($"xrecord-fail h={r.Handle:X} size={r.Xrecord?.XdataSize ?? -1} " +
                $"bit={r.Xrecord?.FailBit ?? -1} items={r.Xrecord?.Items.Count ?? 0} codes={codes} reason={reason}");
        }
        foreach (var (k, n) in xrFail.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"xrecord-fail-group n={n} reason={k}");

        Censys("vport", WireVport, objs.Records,
            r => r.Vport is { } c ? (true, c.Captured, c.ValuesCommitted) : (false, false, false));
        Censys("appid", WireAppid, objs.Records,
            r => r.Appid is { } c ? (true, c.Captured, false) : (false, false, false));
        Censys("scale", WireScale, objs.Records,
            r => r.Scale is { } c ? (true, c.Captured, false) : (false, false, false));
    }

    private static void Censys(string name, short type, IReadOnlyList<DwgObjectRecord> records,
        Func<DwgObjectRecord, (bool HasFamily, bool Axis1, bool Axis2)> probe)
    {
        int total = 0, none = 0, a1 = 0, a2 = 0, rejected = 0;
        var rejectedHandles = new List<string>();
        foreach (var r in records)
        {
            if (r.TypeCode != type) continue;
            total++;
            var (has, axis1, axis2) = probe(r);
            if (!has) { none++; continue; }
            if (axis1) a1++;
            else if (axis2) a2++;
            else { rejected++; rejectedHandles.Add($"h={r.Handle:X}"); }
        }
        Console.WriteLine($"{name} total={total} axis1={a1} axis2={a2} rejected={rejected} none={none}" +
            (rejectedHandles.Count > 0 ? " rejected:" + string.Join(",", rejectedHandles.Take(12)) : ""));
    }
}
