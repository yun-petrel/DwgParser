using System.IO.Compression;
using System.Text;

using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public static class PngEncoder
{
    public static void Save(PixelBuffer buffer, string path)
    {
        using var fs = File.Create(path);
        Save(buffer, fs);
    }

    public static void Save(PixelBuffer buffer, Stream stream)
    {
        using var bw = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        WriteChunk(bw, "IHDR", BuildIhdr(buffer.Width, buffer.Height));

        byte[] raw = BuildRawScanlines(buffer);
        byte[] compressed = ZlibCompress(raw);
        WriteChunk(bw, "IDAT", compressed);

        WriteChunk(bw, "IEND", Array.Empty<byte>());
    }

    public static byte[] Encode(PixelBuffer buffer)
    {
        using var ms = new MemoryStream();
        Save(buffer, ms);
        return ms.ToArray();
    }


    private static byte[] BuildIhdr(int width, int height)
    {
        var data = new byte[13];
        WriteInt32Big(data, 0, width);
        WriteInt32Big(data, 4, height);
        data[8] = 8;
        data[9] = 6;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        return data;
    }

    private static byte[] BuildRawScanlines(PixelBuffer buffer)
    {
        int rowBytes = 1 + buffer.Width * 4;
        var raw = new byte[buffer.Height * rowBytes];
        var src = buffer.Pixels;
        int stride = buffer.Width * 4;
        for (int y = 0; y < buffer.Height; y++)
        {
            int rowOff = y * rowBytes;
            raw[rowOff] = 0;
            int s0 = y * stride;
            int d0 = rowOff + 1;
            for (int x = 0; x < buffer.Width; x++)
            {
                int si = s0 + x * 4;
                int di = d0 + x * 4;
                raw[di]     = src[si + 2];
                raw[di + 1] = src[si + 1];
                raw[di + 2] = src[si];
                raw[di + 3] = src[si + 3];
            }
        }
        return raw;
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }
        uint adler = Adler32(data);
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        const uint MOD = 65521;
        uint a = 1, b = 0;
        for (int i = 0; i < data.Length; i++)
        {
            a = (a + data[i]) % MOD;
            b = (b + a) % MOD;
        }
        return (b << 16) | a;
    }

    private static void WriteChunk(BinaryWriter bw, string type, byte[] data)
    {
        byte[] lenBytes = new byte[4];
        WriteInt32Big(lenBytes, 0, data.Length);
        bw.Write(lenBytes);

        bw.Write(Encoding.ASCII.GetBytes(type));

        bw.Write(data);

        byte[] crcInput = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, 0, 4, crcInput, 0);
        Buffer.BlockCopy(data, 0, crcInput, 4, data.Length);
        uint crc = Crc32(crcInput);
        byte[] crcBytes = new byte[4];
        WriteInt32Big(crcBytes, 0, (int)crc);
        bw.Write(crcBytes);
    }

    private static void WriteInt32Big(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        uint[] table = Crc32Table;
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
            crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] Crc32Table = InitCrcTable();
    private static uint[] InitCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}