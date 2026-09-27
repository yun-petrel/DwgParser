using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    private static void DecodeDictionary(BitReader br, bool r2010Plus,
        DwgVersion version, DwgObjectRecord record)
    {
        bool r2004Plus = version.IsR2004Plus();
        bool r2013Plus = version >= DwgVersion.AC1027;
        uint inlineRl = 0;
        if (!r2010Plus) inlineRl = br.ReadRL();
        br.ReadHandle();
        if (!TrySkipEed(br)) return;
        long numReactors = br.ReadBL();
        bool xdicMissing = true;
        if (r2004Plus)
        {
            xdicMissing = br.ReadB();
            if (r2013Plus) br.ReadB();
        }
        else xdicMissing = false;
        long numitems = br.ReadBL();
        int cloning = br.ReadBS();
        int isHardOwner = br.ReadRC();
        if (numReactors is < 0 or > 100000 || numitems is < 0 or > 1_000_000)
            return;
        record.DictionaryNumItems = (int)numitems;
        record.DictionaryCloning = cloning;
        record.DictionaryIsHardOwner = isHardOwner;

        if (version >= DwgVersion.AC1021)
        {
            try
            {
                var keys = new List<string>((int)numitems);
                for (long i = 0; i < numitems; i++) keys.Add(br.ReadTV());
                for (long i = 0; i < numitems; i++)
                    record.DictionaryEntries.Add((keys[(int)i], 0));

                long itemHs = r2010Plus
                    ? (record.HasHandleStream ? record.HandleStreamStartBit : -1)
                    : (long)record.BodyStart * 8 + inlineRl;
                FillDictionaryItemHandles(br, record, numReactors, xdicMissing, numitems, itemHs);
            }
            catch { record.DictionaryEntries.Clear();  }
        }
        else if (version >= DwgVersion.AC1015 && version < DwgVersion.AC1021 && numitems > 0)
        {
            int strBefore = record.StringFields.Count;
            int entBefore = record.DictionaryEntries.Count;
            bool ok = true;
            try
            {
                for (long i = 0; i < numitems; i++)
                {
                    long ks = br.BitPosition;
                    string key = br.ReadTV();
                    int bits = (int)(br.BitPosition - ks);
                    if (bits <= 0 || bits > 8192) { ok = false; break; }
                    record.StringFields.Add(new DwgObjectRecord.StringField(ks, bits, key));
                    record.DictionaryEntries.Add((key, 0));
                }
            }
            catch { ok = false; }
            long dataEnd = (long)record.BodyStart * 8 + inlineRl;
            if (!ok || br.BitPosition != dataEnd)
            {
                while (record.StringFields.Count > strBefore)
                    record.StringFields.RemoveAt(record.StringFields.Count - 1);
                while (record.DictionaryEntries.Count > entBefore)
                    record.DictionaryEntries.RemoveAt(record.DictionaryEntries.Count - 1);
            }
            else
            {
                long hsSave = br.BitPosition;
                try
                {
                    br.ReadHandle();
                    for (long k = 0; k < numReactors; k++) br.ReadHandle();
                    if (!xdicMissing) br.ReadHandle();
                    for (long k = 0; k < numitems && k < record.DictionaryEntries.Count; k++)
                    {
                        var h = br.ReadHandle();
                        record.DictionaryEntries[(int)k] =
                            (record.DictionaryEntries[(int)k].Key, h.Resolve(record.Handle));
                    }
                }
                catch {  }
                finally { br.SetPosition((int)(hsSave >> 3), (int)(hsSave & 7)); }
            }
        }
    }

    private static void FillDictionaryItemHandles(BitReader br, DwgObjectRecord record,
        long numReactors, bool xdicMissing, long numitems, long itemHs)
    {
        long save = br.BitPosition;
        if (numitems <= 0 || itemHs <= save || itemHs >= (long)record.BodyEnd * 8) return;
        try
        {
            br.SetPosition((int)(itemHs >> 3), (int)(itemHs & 7));
            br.ReadHandle();
            for (long k = 0; k < numReactors; k++) br.ReadHandle();
            if (!xdicMissing) br.ReadHandle();
            for (long k = 0; k < numitems && k < record.DictionaryEntries.Count; k++)
            {
                var vh = br.ReadHandle().Resolve(record.Handle);
                if (vh == 0) continue;
                record.DictionaryEntries[(int)k] = (record.DictionaryEntries[(int)k].Key, vh);
            }
            if (br.BitPosition > (long)record.BodyEnd * 8)
                for (int k = 0; k < record.DictionaryEntries.Count; k++)
                    record.DictionaryEntries[k] = (record.DictionaryEntries[k].Key, 0);
        }
        catch {  }
        finally { br.SetPosition((int)(save >> 3), (int)(save & 7)); }
    }
}