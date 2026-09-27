using System.Text;

namespace DwgParser.Core.IO;

public sealed class BitWriter
{
    private byte[] _buf;
    private int _bytePos;
    private int _bitPos;

    public BitWriter(int initialCapacity = 4096)
    {
        _buf = new byte[Math.Max(initialCapacity, 16)];
    }

    public long BitPosition => (long)_bytePos * 8 + _bitPos;

    public int BytePosition => _bytePos;

    public int BitOffset => _bitPos;


    internal static BitWriter? DebugWatch;
    public void WriteBit(bool value)
    {
        if (DebugWatch == this && System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine($"WB pos={BitPosition} v={(value ? 1 : 0)}\n" + new System.Diagnostics.StackTrace(true).ToString().Split('\n')[2].Trim());
        EnsureCapacity(1);
        if (value)
            _buf[_bytePos] |= (byte)(1 << (7 - _bitPos));
        if (++_bitPos == 8)
        {
            _bitPos = 0;
            _bytePos++;
        }
    }

    public void WriteBits(uint value, int count)
    {
        for (int i = count - 1; i >= 0; i--)
            WriteBit(((value >> i) & 1) != 0);
    }

    public void WriteBits(int value, int count) => WriteBits(unchecked((uint)value), count);

    public void WriteBitsFrom(BitReader src, int count)
    {
        for (int i = 0; i < count; i++)
            WriteBit(src.ReadBit() != 0);
    }

    public void WriteBitsFrom(BitWriter src, long count)
    {
        for (long i = 0; i < count; i++)
            WriteBit(src.GetBit(i));
    }

    public void OverwriteBits(long patchBit, uint value, int count)
    {
        for (int i = 0; i < count; i++)
        {
            long pos = patchBit + i;
            int idx = (int)(pos >> 3);
            int bit = 7 - (int)(pos & 7);
            uint mask = 1u << bit;
            uint vbit = ((value >> (count - 1 - i)) & 1u) << bit;
            _buf[idx] = (byte)((_buf[idx] & ~mask) | vbit);
        }
    }

    private bool GetBit(long bit)
        => ((_buf[bit >> 3] >> (7 - (int)(bit & 7))) & 1) != 0;

    public long TrimTrailingZeroBits()
    {
        long end = BitPosition;
        long last = end - 1;
        while (last >= 0 && !GetBit(last))
            last--;
        if (last < 0)
            return 0;
        long trimmed = end - (last + 1);
        if (trimmed <= 0)
            return 0;
        _bytePos = (int)(last >> 3);
        _bitPos = (int)(last & 7) + 1;
        if (_bitPos == 8) { _bitPos = 0; _bytePos++; }
        return trimmed;
    }

    public long CountTrailingZeroBits()
    {
        long end = BitPosition;
        long last = end - 1;
        while (last >= 0 && !GetBit(last))
            last--;
        return end - (last + 1);
    }

    public void TrimLastBits(long count)
    {
        if (count <= 0)
            return;
        long end = BitPosition;
        if (count > end)
            count = end;
        long keep = end - count;
        _bytePos = (int)(keep >> 3);
        _bitPos = (int)(keep & 7);
        int byteIndex = _bytePos;
        if (byteIndex < _buf.Length)
        {
            int keepBits = _bitPos;
            byte mask = keepBits == 0 ? (byte)0 : (byte)(0xFF << (8 - keepBits));
            _buf[byteIndex] = (byte)(_buf[byteIndex] & mask);
            for (int i = byteIndex + 1; i < _buf.Length; i++) _buf[i] = 0;
        }
    }

    public void SkipBits(int count)
    {
        for (int i = 0; i < count; i++)
            WriteBit(false);
    }

    public void AlignByte()
    {
        if (_bitPos != 0)
        {
            _bitPos = 0;
            _bytePos++;
        }
    }

    public void Align() => AlignByte();


    public void WriteRC(byte value) => WriteBits(value, 8);

    public void WriteRS(ushort value)
    {
        WriteRC((byte)value);
        WriteRC((byte)(value >> 8));
    }

    public void WriteRS(short value) => WriteRS(unchecked((ushort)value));

    public void WriteRL(uint value)
    {
        WriteRS((ushort)value);
        WriteRS((ushort)(value >> 16));
    }

    public void WriteRL(int value) => WriteRL(unchecked((uint)value));

    public void PatchRL(long bitPos, uint value)
    {
        if (bitPos < 0) return;
        for (int p = 0; p < 32; p++)
        {
            long abs = bitPos + p;
            int byteIndex = (int)(abs >> 3);
            int bitOff = 7 - (int)(abs & 7);
            if (byteIndex >= _buf.Length) break;
            int vb = (p >> 3) * 8 + (7 - (p & 7));
            uint bit = (value >> vb) & 1;
            if (bit != 0)
                _buf[byteIndex] |= (byte)(1 << bitOff);
            else
                _buf[byteIndex] &= (byte)~(1 << bitOff);
        }
    }

    public void WriteRLL(ulong value)
    {
        WriteRL((uint)value);
        WriteRL((uint)(value >> 32));
    }

    public void WriteRLL(long value) => WriteRLL(unchecked((ulong)value));

    public void WriteRD(double value) => WriteRLL(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

    public void WriteRS_BE(ushort value) => WriteBits(value, 16);


    public void WriteB(bool value) => WriteBit(value);

    public void WriteBB(int value) => WriteBits((uint)value & 3, 2);

    public void WriteBS(short value)
    {
        ushort l = unchecked((ushort)value);
        if (l > 256) { WriteBB(0); WriteRS(l); }
        else if (value == 0) WriteBB(2);
        else if (value == 256) WriteBB(3);
        else { WriteBB(1); WriteRC((byte)l); }
    }

    public void WriteBL(int value)
    {
        if (value == 0) WriteBB(2);
        else if (value == 256) WriteBB(3);
        else if (value is >= 0 and <= 255) { WriteBB(1); WriteRC((byte)value); }
        else { WriteBB(0); WriteRL(value); }
    }

    public void WriteBLL(ulong value)
    {
        int len = 0;
        if (value != 0)
        {
            len = 1;
            while (len < 7 && (value >> (8 * len)) != 0) len++;
        }
        WriteB((len & 4) != 0);
        WriteB((len & 2) != 0);
        WriteB((len & 1) != 0);
        for (int i = 0; i < len; i++) WriteRC((byte)(value >> (8 * i)));
    }

    public void WriteBD(double value)
    {
        if (value == 0.0) WriteBB(2);
        else if (value == 1.0) WriteBB(1);
        else { WriteBB(0); WriteRD(value); }
    }

    public void WriteDD(double value, double defaultValue)
    {
        if (value.Equals(defaultValue)) WriteBB(0);
        else { WriteBB(3); WriteRD(value); }
    }

    public void WriteBD0(double value)
    {
        if (value == 0.0) WriteBit(true);
        else { WriteBit(false); WriteBD(value); }
    }

    public void WriteBT(double value)
    {
        if (value == 0.0) WriteBit(true);
        else { WriteBit(false); WriteBD(value); }
    }

    public void WriteBE(double x, double y, double z)
    {
        if (x == 0.0 && y == 0.0 && z == 1.0) WriteBit(true);
        else { WriteBit(false); WriteBD(x); WriteBD(y); WriteBD(z); }
    }

    public void WriteBEExplicit(double x, double y, double z)
    {
        WriteBit(false);
        WriteBD(x); WriteBD(y); WriteBD(z);
    }

    public void WriteBOT(int value)
    {
        if (value is >= 0 and <= 0xFF) { WriteBB(0); WriteRC((byte)value); }
        else if (value is >= 0x1F0 and <= 0x2EF) { WriteBB(1); WriteRC((byte)(value - 0x1F0)); }
        else { WriteBB(2); WriteRS(unchecked((ushort)value)); }
    }

    public void WriteMS(uint value)
    {
        if (value < 0x8000) WriteRS((ushort)value);
        else
        {
            WriteRS((ushort)((value & 0x7FFF) | 0x8000));
            WriteRS((ushort)((value >> 15) & 0x7FFF));
        }
    }

    public void WriteMC(int value)
    {
        const uint maxMagnitude = 0x7FFFFFF;
        uint magnitude = value < 0 ? unchecked((uint)(-(long)value)) : (uint)value;
        if (magnitude > maxMagnitude)
            throw new ArgumentOutOfRangeException(nameof(value), "MC 编码仅支持 |v| < 2^27。");
        bool negative = value < 0;
        while (magnitude >= 0x40)
        {
            WriteRC((byte)((magnitude & 0x7F) | 0x80));
            magnitude >>= 7;
        }
        WriteRC((byte)((negative ? 0x40u : 0u) | magnitude));
    }

    public void WriteUMC(uint value)
    {
        while (value >= 0x80)
        {
            WriteRC((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        WriteRC((byte)value);
    }


    public void WriteHandleRef(int code, ulong value)
    {
        int counter = 0;
        for (ulong t = value; t != 0; t >>= 8) counter++;
        if (counter > 8) throw new ArgumentOutOfRangeException(nameof(value));
        WriteRC((byte)((code << 4) | counter));
        for (int i = counter - 1; i >= 0; i--)
            WriteRC((byte)((value >> (i * 8)) & 0xFF));
    }

    public void WriteHandle(ulong handle) => WriteHandleRef(0, handle);

    public void WriteTV(string? value, bool unicode)
    {
        if (string.IsNullOrEmpty(value)) { WriteBS(0); return; }
        if (unicode)
        {
            WriteBS(unchecked((short)(value.Length + 1)));
            foreach (char c in value) WriteRS(c);
            WriteRS((ushort)0);
            return;
        }
        byte[] bytes = EncodeText(value);
        WriteBS(unchecked((short)bytes.Length));
        foreach (var b in bytes) WriteRC(b);
    }

    public void WriteTV(string? value) => WriteTV(value, false);

    internal static byte[] EncodeText(string value)
    {
        bool ascii = true;
        foreach (var c in value)
            if (c > 0x7f) { ascii = false; break; }
        if (ascii)
            return Encoding.UTF8.GetBytes(value);
        try
        {
            var gbk = Encoding.GetEncoding("GBK").GetBytes(value);
            if (Encoding.GetEncoding("GBK").GetString(gbk) == value)
                return gbk;
        }
        catch (ArgumentException) {  }
        return Encoding.UTF8.GetBytes(value);
    }


    public byte[] ToArray()
    {
        var result = new byte[_bitPos == 0 ? _bytePos : _bytePos + 1];
        Buffer.BlockCopy(_buf, 0, result, 0, Math.Min(_buf.Length, result.Length));
        return result;
    }

    public void WriteObjectCRC(int startByte)
    {
        AlignByte();
        int len = _bytePos - startByte;
        ushort crc = Crc.Compute16Dwg(_buf, startByte, len);
        WriteRS(crc);
    }

    internal ReadOnlySpan<byte> BufferSpan => _buf;

    private void EnsureCapacity(int extraBytes)
    {
        int needed = _bytePos + extraBytes + 1;
        if (needed <= _buf.Length) return;
        int size = _buf.Length;
        while (size < needed) size *= 2;
        Array.Resize(ref _buf, size);
    }
}
