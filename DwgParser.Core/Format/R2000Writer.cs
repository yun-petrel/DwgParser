using System.Text;
using DwgParser.Core;

namespace DwgParser.Core.Format;

public static class R2000Writer
{
    public static readonly byte[] FileHeaderSentinel =
    {
        0x95, 0xA0, 0x4E, 0x28, 0x99, 0x82, 0x1A, 0xE5,
        0x5E, 0x41, 0xE0, 0x5F, 0x9D, 0x3A, 0x4D, 0x00,
    };

    public static readonly string[] StandardSectionNames =
    {
        "Header", "Classes", "Handles", "ObjFreeSpace", "Template", "AuxHeader"
    };

    public sealed class SectionEntry
    {
        public string Name { get; }
        public byte[] Data { get; }
        public int RecordNumber { get; internal set; }
        public bool InLocator { get; internal set; } = true;
        public SectionEntry(string name, byte[] data) { Name = name; Data = data; }
    }

    public static byte[] Build(DwgVersion version, IReadOnlyList<SectionEntry> sections)
    {
        string sig = version switch
        {
            DwgVersion.AC1015 => "AC1015",
            _ => "AC1015",
        };

        int locatorCount = 0;
        foreach (var s in sections)
            if (s.InLocator) locatorCount++;

        int headerSize = 0x19 + locatorCount * 9 + 2 + FileHeaderSentinel.Length;
        int pos = headerSize;

        var addresses = new int[sections.Count];
        for (int i = 0; i < sections.Count; i++)
        {
            addresses[i] = pos;
            pos += sections[i].Data.Length;
        }

        int totalSize = pos;
        var file = new byte[totalSize];

        Encoding.ASCII.GetBytes(sig, 0, 6, file, 0);
        file[0x0B] = 0x0F;
        file[0x0C] = 0x01;
        file[0x11] = 0x1F;
        file[0x12] = 0x69;
        file[0x13] = 0x27; file[0x14] = 0x00;

        file[0x15] = (byte)locatorCount;
        file[0x16] = 0;
        file[0x17] = 0;
        file[0x18] = 0;

        int recPos = 0x19;
        for (int i = 0; i < sections.Count; i++)
        {
            if (!sections[i].InLocator) continue;
            file[recPos] = (byte)sections[i].RecordNumber;
            WriteUInt32Le(file, recPos + 1, (uint)addresses[i]);
            WriteUInt32Le(file, recPos + 5, (uint)sections[i].Data.Length);
            recPos += 9;
        }

        ushort crc = Crc.Compute16Dwg(file, 0, recPos);
        file[recPos] = (byte)crc;
        file[recPos + 1] = (byte)(crc >> 8);
        recPos += 2;

        Buffer.BlockCopy(FileHeaderSentinel, 0, file, recPos, FileHeaderSentinel.Length);

        for (int i = 0; i < sections.Count; i++)
            Buffer.BlockCopy(sections[i].Data, 0, file, addresses[i], sections[i].Data.Length);

        return file;
    }

    public static byte[] BuildDefaultAuxHeader()
    {
        var a = new byte[123];
        int p = 0;
        void W(byte[] src) { Buffer.BlockCopy(src, 0, a, p, src.Length); p += src.Length; }
        void W16(ushort v) { W([(byte)v, (byte)(v >> 8)]); }
        void W32(uint v) { W([(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)]); }

        W([0xFF, 0x77, 0x01]);
        W16(0x001F);
        W16(0x0069);
        W32(0);
        W32(0xFFFFFFFF);
        W16(0);
        W16(0);
        W32(0);
        W16(0x001F);
        W16(0x0069);
        W16(0x001F);
        W16(0x0069);
        W16(4); W16(0x565); W16(0); W16(0); W16(2); W16(1);
        W32(0); W32(0); W32(0); W32(256); W32(393218);
        W32(0); W32(0);
        W32(0); W32(0);
        W32(0); W32(0);
        W16(0);
        W16(0);
        W32(0); W32(0); W32(0);
        W32(0);
        W32(0); W32(0);
        return a;
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)value;
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }
}
