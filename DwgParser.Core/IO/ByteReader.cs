using System.Buffers.Binary;
using System.Text;

namespace DwgParser.Core.IO;

public sealed class ByteReader
{
    private readonly byte[] _data;

    public ByteReader(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Position = 0;
    }

    public byte[] Data => _data;
    public int Length => _data.Length;
    public int Position { get; set; }
    public int Remaining => _data.Length - Position;
    public bool EndOfStream => Position >= _data.Length;

    public void Seek(int offset)
    {
        if (offset < 0 || offset > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        Position = offset;
    }

    public byte ReadByte()
    {
        if (Position >= _data.Length)
            throw new EndOfStreamException();
        return _data[Position++];
    }

    public byte[] ReadBytes(int count)
    {
        if (count < 0 || Position + count > _data.Length)
            throw new EndOfStreamException($"Cannot read {count} bytes at offset {Position}.");
        var result = new byte[count];
        Array.Copy(_data, Position, result, 0, count);
        Position += count;
        return result;
    }

    public ushort ReadUInt16()
    {
        var b = ReadBytes(2);
        return (ushort)(b[0] | (b[1] << 8));
    }

    public uint ReadUInt32()
    {
        var b = ReadBytes(4);
        return (uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24));
    }

    public ulong ReadUInt64()
    {
        var lo = (ulong)ReadUInt32();
        var hi = (ulong)ReadUInt32();
        return lo | (hi << 32);
    }

    public short ReadInt16() => unchecked((short)ReadUInt16());
    public int ReadInt32() => unchecked((int)ReadUInt32());
    public long ReadInt64() => unchecked((long)ReadUInt64());

    public double ReadDouble()
    {
        var b = ReadBytes(8);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(b);
        return BitConverter.ToDouble(b, 0);
    }

    public float ReadSingle()
    {
        var b = ReadBytes(4);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(b);
        return BitConverter.ToSingle(b, 0);
    }

    public string ReadCString()
    {
        int start = Position;
        while (Position < _data.Length && _data[Position] != 0)
            Position++;
        var s = Encoding.ASCII.GetString(_data, start, Position - start);
        if (Position < _data.Length) Position++;
        return s;
    }

    public string ReadLengthPrefixedString()
    {
        int len = ReadUInt16();
        if (len == 0 || Position + len > _data.Length) return string.Empty;
        return Encoding.ASCII.GetString(_data, Position, len).TrimEnd('\0');
    }

    public string ReadUnicodeString()
    {
        var sb = new StringBuilder();
        while (Position + 1 < _data.Length)
        {
            ushort c = ReadUInt16();
            if (c == 0) break;
            sb.Append((char)c);
        }
        return sb.ToString();
    }

    public void Skip(int count) => Position += count;
}
