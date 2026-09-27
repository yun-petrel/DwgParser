namespace DwgParser.Core.Compression;

public static partial class RsEncoder
{
    internal const int MaxEncodableDistance = 49151;

    private const int MinMatch = 3;
    private const int MaxMatchLength = 65535;
    private const int HashBits = 15;
    private const int HashSize = 1 << HashBits;
    private const int HashMask = HashSize - 1;
    private const int MaxChain = 128;

    public static byte[] Compress(byte[] data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (data.Length < 4)
            throw new ArgumentOutOfRangeException(nameof(data),
                $"RS 压缩无法编码 {data.Length} 字节（需 ≥ 4）。");

        var store = Store(data);
        var lz = CompressStream(data);
        return lz.Length < store.Length ? lz : store;
    }

    public static byte[] CompressStream(byte[] data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (data.Length < 4)
            throw new ArgumentOutOfRangeException(nameof(data));
        return new Lz77SectionCompressor(data).Run();
    }

    private sealed class Lz77SectionCompressor
    {
        private readonly byte[] _src;
        private readonly int _n;
        private readonly List<byte> _out;
        private readonly int[] _head = new int[HashSize];
        private readonly int[] _prev;

        internal Lz77SectionCompressor(byte[] src)
        {
            _src = src;
            _n = src.Length;
            _out = new List<byte>(src.Length);
            _prev = new int[src.Length];
            Array.Fill(_head, -1);
        }

        internal byte[] Run()
        {
            int pos = 0;
            int litStart = 0;
            bool lastWasMatch = false;
            int trailIdx = -1;

            while (pos < _n)
            {
                var (len, dist) = FindMatch(pos);
                if (len < MinMatch)
                {
                    Insert(pos);
                    pos++;
                    continue;
                }

                int gap = pos - litStart;
                if (!lastWasMatch)
                {
                    if (gap < 4)
                    {
                        Insert(pos);
                        pos++;
                        continue;
                    }
                    EmitLiteralRun(gap);
                    EmitLiteralBytes(litStart, gap);
                }
                else if (gap == 0)
                {
                }
                else if (gap <= 3)
                {
                    PatchTrail(trailIdx, gap);
                    EmitLiteralBytes(litStart, gap);
                }
                else
                {
                    EmitLiteralRun(gap);
                    EmitLiteralBytes(litStart, gap);
                }

                trailIdx = EmitMatch(len, dist);
                for (int i = pos; i < pos + len; i++) Insert(i);
                pos += len;
                litStart = pos;
                lastWasMatch = true;
            }

            int tail = _n - litStart;
            if (!lastWasMatch)
            {
                EmitLiteralRun(tail);
                EmitLiteralBytes(0, tail);
            }
            else if (tail == 0)
            {
            }
            else if (tail <= 3)
            {
                PatchTrail(trailIdx, tail);
                EmitLiteralBytes(litStart, tail);
            }
            else
            {
                EmitLiteralRun(tail);
                EmitLiteralBytes(litStart, tail);
            }

            _out.Add(0x11);
            return _out.ToArray();
        }


        private void EmitLiteralRun(int len)
        {
            if (len <= 18)
            {
                _out.Add((byte)(len - 3));
            }
            else
            {
                _out.Add(0x00);
                EmitExtendedCount(len - 18);
            }
        }

        private void EmitExtendedCount(int rem)
        {
            int k = (rem - 1) / 0xFF;
            int last = rem - 0xFF * k;
            for (int i = 0; i < k; i++) _out.Add(0x00);
            _out.Add((byte)last);
        }

        private void EmitLiteralBytes(int start, int count)
        {
            for (int i = 0; i < count; i++) _out.Add(_src[start + i]);
        }

        private int EmitMatch(int len, int dist)
        {
            if (dist <= 1024 && len <= 14)
            {
                int d = dist - 1;
                int idx = _out.Count;
                _out.Add((byte)(((len + 1) << 4) | ((d & 3) << 2)));
                _out.Add((byte)(d >> 2));
                return idx;
            }

            if (dist <= 16384)
            {
                int d = dist - 1;
                if (len <= 33)
                {
                    _out.Add((byte)(0x20 | (len - 2)));
                }
                else
                {
                    _out.Add(0x20);
                    EmitExtendedCount(len - 33);
                }
                int idx = _out.Count;
                _out.Add((byte)((d & 0x3F) << 2));
                _out.Add((byte)((d >> 6) & 0xFF));
                return idx;
            }

            int basis = dist - 0x4000;
            int bit3 = (basis >> 14) & 1;
            int opBase = 0x10 | (bit3 << 3);
            if (len <= 9)
            {
                _out.Add((byte)(opBase | (len - 2)));
            }
            else
            {
                _out.Add((byte)opBase);
                EmitExtendedCount(len - 9);
            }
            int trail = _out.Count;
            _out.Add((byte)((basis & 0x3F) << 2));
            _out.Add((byte)((basis >> 6) & 0xFF));
            return trail;
        }

        private void PatchTrail(int idx, int count)
        {
            _out[idx] = (byte)((_out[idx] & 0xFC) | (count & 3));
        }


        private void Insert(int pos)
        {
            if (pos + 2 >= _n) return;
            int h = Hash3(pos);
            _prev[pos] = _head[h];
            _head[h] = pos;
        }

        private int Hash3(int pos)
            => (_src[pos] * 961 + _src[pos + 1] * 31 + _src[pos + 2]) & HashMask;

        private (int Len, int Dist) FindMatch(int pos)
        {
            if (pos + 2 >= _n) return (0, 0);

            int cand = _head[Hash3(pos)];
            int bestLen = 0, bestDist = 0, chain = 0;
            int maxLen = Math.Min(_n - pos, MaxMatchLength);

            while (cand >= 0 && chain < MaxChain)
            {
                int dist = pos - cand;
                if (dist > MaxEncodableDistance) break;

                int len = 0;
                while (len < maxLen && _src[cand + len] == _src[pos + len]) len++;

                if (len > bestLen && len >= MinMatch && IsEncodable(len, dist))
                {
                    bestLen = len;
                    bestDist = dist;
                    if (len == maxLen) break;
                }

                cand = _prev[cand];
                chain++;
            }

            return bestLen >= MinMatch ? (bestLen, bestDist) : (0, 0);
        }

        private static bool IsEncodable(int len, int dist)
        {
            if (len < MinMatch || dist < 1 || dist > MaxEncodableDistance) return false;
            if (dist <= 16384) return true;
            return dist < 32768 ? len >= 4 : len >= 3;
        }
    }
}
