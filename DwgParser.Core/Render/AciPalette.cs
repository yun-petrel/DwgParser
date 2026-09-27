namespace DwgParser.Core.Render;

public static class AciPalette
{
    public const int Count = 256;

    public const int ByBlock = 0;

    public const int ByLayer = 256;

    public const int CmcMethodTrueColor = 0xC2;

    public const int CmcMethodByLayer = 0xC0;

    public const int CmcMethodByBlock = 0xC1;

    public const int CmcMethodByAci = 0xC3;

    private static readonly byte[][] Rgb =
    [
        [0,0,0],
        [255,0,0],
        [255,255,0],
        [0,255,0],
        [0,255,255],
        [0,0,255],
        [255,0,255],
        [255,255,255],
        [128,128,128],
        [192,192,192],
        [255,0,0],[255,127,127],[165,0,0],[165,82,82],[127,0,0],[127,63,63],[76,0,0],[76,38,38],[38,0,0],[38,19,19],
        [255,63,0],[255,159,127],[165,41,0],[165,103,82],[127,31,0],[127,79,63],[76,19,0],[76,47,38],[38,9,0],[38,28,19],
        [255,127,0],[255,191,127],[165,82,0],[165,124,82],[127,63,0],[127,95,63],[76,38,0],[76,57,38],[38,19,0],[38,28,19],
        [255,191,0],[255,223,127],[165,124,0],[165,145,82],[127,95,0],[127,111,63],[76,57,0],[76,66,38],[38,28,0],[38,33,19],
        [255,255,0],[255,255,127],[165,165,0],[165,165,82],[127,127,0],[127,127,63],[76,76,0],[76,76,38],[38,38,0],[38,38,19],
        [191,255,0],[223,255,127],[124,165,0],[145,165,82],[95,127,0],[111,127,63],[57,76,0],[66,76,38],[28,38,0],[33,38,19],
        [127,255,0],[191,255,127],[82,165,0],[124,165,82],[63,127,0],[95,127,63],[38,76,0],[57,76,38],[19,38,0],[28,38,19],
        [63,255,0],[159,255,127],[41,165,0],[103,165,82],[31,127,0],[79,127,63],[19,76,0],[47,76,38],[9,38,0],[23,38,19],
        [0,255,0],[125,255,127],[0,165,0],[82,165,82],[0,127,0],[63,127,63],[0,76,0],[38,76,38],[0,38,0],[19,38,19],
        [0,255,63],[127,255,159],[0,165,41],[82,165,103],[0,127,31],[63,127,79],[0,76,19],[38,76,47],[0,38,9],[19,88,23],
        [0,255,127],[127,255,191],[0,165,82],[82,165,124],[0,127,63],[63,127,95],[0,76,38],[38,76,57],[0,38,19],[19,88,28],
        [0,255,191],[127,255,223],[0,165,124],[82,165,145],[0,127,95],[63,127,111],[0,76,57],[38,76,66],[0,38,28],[19,88,88],
        [0,255,255],[127,255,255],[0,165,165],[82,165,165],[0,127,127],[63,127,127],[0,76,76],[38,76,76],[0,38,38],[19,88,88],
        [0,191,255],[127,223,255],[0,124,165],[82,145,165],[0,95,127],[63,111,217],[0,57,76],[38,66,126],[0,28,38],[19,88,88],
        [0,127,255],[127,191,255],[0,82,165],[82,124,165],[0,63,127],[63,95,127],[0,38,76],[38,57,126],[0,19,38],[19,28,88],
        [0,63,255],[127,159,255],[0,41,165],[82,103,165],[0,31,127],[63,79,127],[0,19,76],[38,47,126],[0,9,38],[19,23,88],
        [0,0,255],[127,127,255],[0,0,165],[82,82,165],[0,0,127],[63,63,127],[0,0,76],[38,38,126],[0,0,38],[19,19,88],
        [63,0,255],[159,127,255],[41,0,165],[103,82,165],[31,0,127],[79,63,127],[19,0,76],[47,38,126],[9,0,38],[23,19,88],
        [127,0,255],[191,127,255],[165,0,82],[124,82,165],[63,0,127],[95,63,127],[38,0,76],[57,38,126],[19,0,38],[28,19,88],
        [191,0,255],[223,127,255],[124,0,165],[142,82,165],[95,0,127],[111,63,127],[57,0,76],[66,38,76],[28,0,38],[88,19,88],
        [255,0,255],[255,127,255],[165,0,165],[165,82,165],[127,0,127],[127,63,127],[76,0,76],[76,38,76],[38,0,38],[88,19,88],
        [255,0,191],[255,127,223],[165,0,124],[165,82,145],[127,0,95],[127,63,111],[76,0,57],[76,38,66],[38,0,28],[88,19,88],
        [255,0,127],[255,127,191],[165,0,82],[165,82,124],[127,0,63],[127,63,95],[76,0,38],[76,38,57],[38,0,19],[88,19,28],
        [255,0,63],[255,127,159],[165,0,41],[165,82,103],[127,0,31],[127,63,79],[76,0,19],[76,38,47],[38,0,9],[88,19,23],
        [51,51,51],[102,102,102],[153,153,153],[204,204,204],[204,204,204],[255,255,255],
    ];

    public static (byte R, byte G, byte B) ToRgb(int aci)
        => aci is >= 0 and < Count ? (Rgb[aci][0], Rgb[aci][1], Rgb[aci][2]) : ((byte)0, (byte)0, (byte)0);

    public static string ToHex(int aci)
    {
        var (r, g, b) = ToRgb(aci);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static int TryExactMatch(int r, int g, int b)
    {
        (r, g, b) = (Clamp(r), Clamp(g), Clamp(b));
        for (int i = 1; i < Count; i++)
            if (Rgb[i][0] == r && Rgb[i][1] == g && Rgb[i][2] == b)
                return i;
        return -1;
    }

    public static int FromRgb(int r, int g, int b)
        => FromRgbWithDistance(r, g, b, out _);

    public static int FromRgb(byte r, byte g, byte b) => FromRgb((int)r, (int)g, (int)b);

    public static int FromRgbWithDistance(int r, int g, int b, out int distanceSq)
    {
        (r, g, b) = (Clamp(r), Clamp(g), Clamp(b));
        int bestIdx = -1;
        int bestDist = int.MaxValue;
        for (int i = 1; i < Count; i++)
        {
            int dr = Rgb[i][0] - r;
            int dg = Rgb[i][1] - g;
            int db = Rgb[i][2] - b;
            int d = dr * dr + dg * dg + db * db;
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
                if (d == 0) break;
            }
        }
        distanceSq = bestDist == int.MaxValue ? 0 : bestDist;
        return bestIdx < 0 ? ByLayer : bestIdx;
    }

    public static int FromPackedRgb(uint rgb24)
    {
        rgb24 &= 0x00FFFFFF;
        return FromRgb((int)((rgb24 >> 16) & 0xFF), (int)((rgb24 >> 8) & 0xFF), (int)(rgb24 & 0xFF));
    }

    public static int FromCmcTrueColor(uint cmcRgbValue)
    {
        int method = (int)((cmcRgbValue >> 24) & 0xFF);
        return method == CmcMethodTrueColor ? FromPackedRgb(cmcRgbValue) : ByLayer;
    }

    public static bool TryCmcToRgb(uint cmc, short bsIndex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        int method = (int)((cmc >> 24) & 0xFF);
        uint low = cmc & 0x00FFFFFF;

        switch (method)
        {
            case CmcMethodTrueColor:
                r = (byte)((low >> 16) & 0xFF); g = (byte)((low >> 8) & 0xFF); b = (byte)(low & 0xFF);
                return (r | g | b) != 0;

            case CmcMethodByAci:
                {
                    int aci = (int)(low & 0xFFFF);
                    if (aci is < 1 or > 255) aci = bsIndex;
                    if (aci is >= 1 and <= 255) { (r, g, b) = ToRgb(aci); return (r | g | b) != 0; }
                    return false;
                }

            case CmcMethodByLayer:
            case CmcMethodByBlock:
                if (bsIndex is >= 1 and <= 255) { (r, g, b) = ToRgb(bsIndex); return (r | g | b) != 0; }
                return false;

            default:
                if (bsIndex is >= 1 and <= 255) { (r, g, b) = ToRgb(bsIndex); return (r | g | b) != 0; }
                if (low is >= 1 and <= 0xFF) { (r, g, b) = ToRgb((int)low); return (r | g | b) != 0; }
                r = (byte)((low >> 16) & 0xFF); g = (byte)((low >> 8) & 0xFF); b = (byte)(low & 0xFF);
                return (r | g | b) != 0;
        }
    }

    private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
}
