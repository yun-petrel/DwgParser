using System.Text;

namespace DwgParser.Core.IO;

public sealed class BitReader
{
    private readonly byte[] _data;
    private int _bytePos;
    private int _bitPos;

    public BitReader(byte[] data, int startOffset = 0)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _bytePos = Math.Max(0, Math.Min(startOffset, data.Length));
        _bitPos = 0;
    }

    public int BytePosition => _bytePos;
    public int BitOffset => _bitPos;

    public long BitPosition => (long)_bytePos * 8 + _bitPos;
    public bool EndOfStream => _bytePos >= _data.Length;

    internal void SeekBits(long absoluteBit)
    {
        _bytePos = (int)(absoluteBit >> 3);
        _bitPos = (int)(absoluteBit & 7);
    }

    public byte[] UnderlyingData() => _data;

    public static byte ReadByte(byte[] data, long bitPos)
    {
        ArgumentNullException.ThrowIfNull(data);
        int b = (int)bitPos;
        byte v = 0;
        for (int k = 0; k < 8; k++)
        {
            int idx = b + k;
            int byteIndex = idx >> 3;
            if (byteIndex < 0 || byteIndex >= data.Length) break;
            int bi = 7 - (idx & 7);
            v = (byte)((v << 1) | ((data[byteIndex] >> bi) & 1));
        }
        return v;
    }

    public BitReader? StringStream { get; set; }

    public void SetPosition(int bytePos, int bitOffset)
    {
        _bytePos = Math.Max(0, Math.Min(bytePos, _data.Length));
        _bitPos = Math.Clamp(bitOffset, 0, 7);
    }

    public byte ReadBit()
    {
        if (_bytePos >= _data.Length)
            throw new EndOfStreamException();
        int shift = 7 - _bitPos;
        byte b = (byte)((_data[_bytePos] >> shift) & 1);
        if (++_bitPos == 8)
        {
            _bitPos = 0;
            _bytePos++;
        }
        return b;
    }

    public ulong ReadBits(int count)
    {
        ulong value = 0;
        for (int i = 0; i < count; i++)
            value = (value << 1) | ReadBit();
        return value;
    }

    public void AlignByte()
    {
        if (_bitPos != 0)
        {
            _bitPos = 0;
            _bytePos++;
        }
    }

    public bool ReadB() => ReadBit() != 0;

    public byte ReadBB() => (byte)ReadBits(2);

    public byte ReadRC() => (byte)ReadBits(8);

    public ushort ReadRS()
    {
        ushort lo = ReadRC();
        ushort hi = ReadRC();
        return (ushort)(lo | (hi << 8));
    }

    public ushort ReadRS_BE() => (ushort)(ReadBits(16));

    public uint ReadRL()
    {
        uint lo = ReadRS();
        uint hi = ReadRS();
        return lo | (hi << 16);
    }

    public double ReadRD()
    {
        Span<byte> buf = stackalloc byte[8];
        for (int i = 0; i < 8; i++)
            buf[i] = ReadRC();
        return BitConverter.UInt64BitsToDouble(BitConverter.ToUInt64(buf));
    }

    public short ReadBS()
    {
        byte twoBit = ReadBB();
        return twoBit switch
        {
            0 => unchecked((short)ReadRS()),
            1 => unchecked((short)ReadBits(8)),
            2 => 0,
            3 => 256,
            _ => 0,
        };
    }

    public short ReadBOT()
    {
        byte code = ReadBB();
        return code switch
        {
            0 => (short)ReadRC(),
            1 => (short)(ReadRC() + 0x1F0),
            _ => (short)ReadRS(),
        };
    }

    public int ReadBL()
    {
        byte twoBit = ReadBB();
        return twoBit switch
        {
            0 => unchecked((int)ReadRL()),
            1 => ReadRC(),
            2 => 0,
            3 => 256,
            _ => 0,
        };
    }

    public ulong ReadBLL()
    {
        int len = ((int)ReadBB() << 1) | (ReadB() ? 1 : 0);
        if (len == 0) return 0UL;
        if (len == 1) return ReadRC();
        if (len == 2) return ReadRS();
        if (len == 4) return ReadRL();
        ulong v = 0;
        for (int i = 0; i < len; i++) v |= (ulong)ReadRC() << (8 * i);
        return v;
    }

    public double ReadBD()
    {
        byte twoBit = ReadBB();
        return twoBit switch
        {
            0 => ReadRD(),
            1 => 1.0,
            2 => 0.0,
            _ => double.NaN,
        };
    }

    public double ReadDD(double defaultValue)
    {
        byte twoBit = ReadBB();
        switch (twoBit)
        {
            case 0:
                return defaultValue;
            case 3:
                return ReadRD();
            case 2:
            {
                Span<byte> buf = stackalloc byte[8];
                BitConverter.TryWriteBytes(buf, BitConverter.DoubleToUInt64Bits(defaultValue));
                buf[4] = ReadRC();
                buf[5] = ReadRC();
                buf[0] = ReadRC();
                buf[1] = ReadRC();
                buf[2] = ReadRC();
                buf[3] = ReadRC();
                return BitConverter.UInt64BitsToDouble(BitConverter.ToUInt64(buf));
            }
            default:
            {
                Span<byte> buf = stackalloc byte[8];
                BitConverter.TryWriteBytes(buf, BitConverter.DoubleToUInt64Bits(defaultValue));
                buf[0] = ReadRC();
                buf[1] = ReadRC();
                buf[2] = ReadRC();
                buf[3] = ReadRC();
                return BitConverter.UInt64BitsToDouble(BitConverter.ToUInt64(buf));
            }
        }
    }

    public double ReadBT()
    {
        bool zeroFlag = ReadB();
        return zeroFlag ? 0.0 : ReadBD();
    }

    public (double X, double Y, double Z) ReadBE()
    {
        if (ReadB())
            return (0.0, 0.0, 1.0);
        double x = ReadBD();
        double y = ReadBD();
        double z = ReadBD();
        if (x == 0.0 && y == 0.0)
            z = z <= 0.0 ? -1.0 : 1.0;
        return (x, y, z);
    }

    public int ReadMC()
    {
        uint result = 0;
        bool negative = false;
        for (int shift = 0; shift <= 28; shift += 7)
        {
            byte b = ReadRC();
            if ((b & 0x80) == 0)
            {
                if ((b & 0x40) != 0)
                {
                    negative = true;
                    b &= 0xBF;
                }
                result |= (uint)b << shift;
                return negative ? unchecked((int)(0u - result)) : unchecked((int)result);
            }
            result |= (uint)(b & 0x7F) << shift;
        }
        return unchecked((int)result);
    }

    public uint ReadUMC()
    {
        ulong result = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            byte b = ReadRC();
            ulong chunk = b & 0x7FUL;
            if ((b & 0x80) == 0)
            {
                result |= chunk << Math.Min(shift, 63 - 7);
                return unchecked((uint)result);
            }
            result |= chunk << Math.Min(shift, 63 - 7);
        }
        return unchecked((uint)result);
    }

    public uint ReadMS()
    {
        uint result = 0;
        for (int shift = 0; shift <= 15; shift += 15)
        {
            ushort w = ReadRS();
            if ((w & 0x8000) == 0)
            {
                result |= (uint)w << shift;
                return result;
            }
            result |= (uint)(w & 0x7FFF) << shift;
        }
        return result;
    }

    public bool UnicodeStrings { get; set; }

    public System.Text.Encoding? LegacyEncoding { get; set; }

    public string ReadTV()
    {
        if (StringStream != null)
            return StringStream.ReadTV();
        int len = ReadBS();
        if (len <= 0 || len > _data.Length)
            return string.Empty;
        if (UnicodeStrings)
            return ReadTU(len);
        var bytes = new byte[len];
        for (int i = 0; i < len; i++)
            bytes[i] = ReadRC();
        var text = LegacyEncoding?.GetString(bytes) ?? DecodeText(bytes);
        return text.TrimEnd('\0');
    }

    private string ReadTU(int length)
    {
        if (length > _data.Length / 2)
            return string.Empty;
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = (char)ReadRS();
        return new string(chars).TrimEnd('\0');
    }

    private static readonly Encoding Gbk;
    static BitReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try { Gbk = Encoding.GetEncoding("GBK"); }
        catch { Gbk = Encoding.Latin1; }
    }

    private static string DecodeText(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            try { return Gbk.GetString(bytes); }
            catch { return Encoding.Latin1.GetString(bytes); }
        }
    }

    public DwgHandleReference ReadHandle()
    {
        byte codeAndCounter = ReadRC();
        int code = codeAndCounter >> 4;
        int counter = codeAndCounter & 0x0F;
        ulong value = 0;
        for (int i = 0; i < counter; i++)
            value = (value << 8) | ReadRC();
        return new DwgHandleReference(code, value);
    }


    public (double X, double Y) Read2RD() => (ReadRD(), ReadRD());

    public (double X, double Y, double Z) Read3BD() => (ReadBD(), ReadBD(), ReadBD());

    public (double X, double Y) Read2DD(double defX, double defY)
        => (ReadDD(defX), ReadDD(defY));


    public double ReadBD0() => ReadB() ? 0.0 : ReadBD();

    public double ReadBD1() => ReadB() ? 1.0 : ReadBD();

    public long ReadBL0() => ReadB() ? 0 : ReadBL();

    public long ReadBS1() => ReadB() ? 1 : ReadBS();

    public (double X, double Y) Read2RD0() => ReadB() ? (0.0, 0.0) : Read2RD();

    public (double X, double Y, double Z) Read3BD1() => ReadB() ? (1.0, 1.0, 1.0) : Read3BD();
}

public readonly struct DwgHandleReference
{
    public DwgHandleReference(int code, ulong value)
    {
        Code = code;
        Value = value;
    }

    public int Code { get; }
    public ulong Value { get; }

    public ulong Resolve(ulong ownerHandle) => Code switch
    {
        6 => ownerHandle + 1,
        8 => ownerHandle - 1,
        10 => ownerHandle + Value,
        12 => ownerHandle - Value,
        14 => ownerHandle,
        _ => Value,
    };

    public override string ToString() => $"[{Code}] {Value:X}";
}