namespace DwgParser.Core;

public static class Crc
{
    private static uint[]? _crcTable;

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320u;
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    private static uint[] Table => _crcTable ??= BuildTable();

    public static uint Compute32(byte[] data, int offset, int count, uint seed = 0)
    {
        uint crc = seed ^ 0xFFFFFFFFu;
        int end = offset + count;
        for (int i = offset; i < end; i++)
            crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    public static uint Compute32(byte[] data) => Compute32(data, 0, data.Length);

    public static byte Compute8(byte[] data, int offset, int count)
    {
        byte sum = 0;
        int end = offset + count;
        for (int i = offset; i < end; i++)
            sum += data[i];
        return sum;
    }

    private static ushort[]? _crc16Table;

    private static ushort[] BuildCrc16Table()
    {
        var table = new ushort[256];
        const ushort polynomial = 0x8408;
        for (ushort i = 0; i < 256; i++)
        {
            ushort crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ polynomial) : (ushort)(crc >> 1);
            table[i] = crc;
        }
        return table;
    }

    public static ushort Compute16(byte[] data, int offset, int count, ushort seed = 0xC0C1)
    {
        var table = _crc16Table ??= BuildCrc16Table();
        ushort crc = seed;
        int end = offset + count;
        for (int i = offset; i < end; i++)
            crc = (ushort)((crc >> 8) ^ table[(crc ^ data[i]) & 0xFF]);
        return crc;
    }

    private static ushort[]? _crc16DwgTable;

    private static ushort[] BuildCrc16DwgTable()
    {
        var table = new ushort[256];
        const ushort polynomial = 0xA001;
        for (ushort i = 0; i < 256; i++)
        {
            ushort crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ polynomial) : (ushort)(crc >> 1);
            table[i] = crc;
        }
        return table;
    }

    public static ushort Compute16Dwg(byte[] data, int offset, int count, ushort seed = 0xC0C1)
    {
        var table = _crc16DwgTable ??= BuildCrc16DwgTable();
        ushort crc = seed;
        int end = offset + count;
        for (int i = offset; i < end; i++)
            crc = (ushort)((crc >> 8) ^ table[(crc ^ data[i]) & 0xFF]);
        return crc;
    }
}
