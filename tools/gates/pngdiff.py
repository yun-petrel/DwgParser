import struct, sys, zlib


def pixels(path):
    d = open(path, 'rb').read()
    pos = 8
    w = h = None
    ch = 3
    idat = b''
    while pos < len(d):
        ln, typ = struct.unpack('>I4s', d[pos:pos + 8])
        if typ == b'IHDR':
            w, h = struct.unpack('>II', d[pos + 8:pos + 16])
            bitdepth, color = d[pos + 16], d[pos + 17]
            if bitdepth != 8 or color not in (2, 6):
                raise SystemExit(f"{path}: 仅支持 8bit RGB/RGBA，实际 {bitdepth}bit/{color}")
            ch = 4 if color == 6 else 3
        if typ == b'IDAT':
            idat += d[pos + 8:pos + 8 + ln]
        pos += 12 + ln
    raw = zlib.decompress(idat)
    stride = w * ch + 1
    px = bytearray(w * h * 3)
    prev = bytearray(w * ch)
    for y in range(h):
        row = raw[y * stride:(y + 1) * stride]
        f = row[0]
        cur = bytearray(row[1:])
        for i in range(len(cur)):
            a = cur[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 1:
                cur[i] = (cur[i] + a) & 255
            elif f == 2:
                cur[i] = (cur[i] + b) & 255
            elif f == 3:
                cur[i] = (cur[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                cur[i] = (cur[i] + (a if pa <= pb and pa <= pc else (b if pb <= pc else c))) & 255
        for x in range(w):
            o = x * ch
            px[(y * w + x) * 3:(y * w + x) * 3 + 3] = cur[o:o + 3]
        prev = cur
    return w, h, px


def main():
    a, b, threshold = sys.argv[1], sys.argv[2], int(sys.argv[3])
    wa, ha, pa = pixels(a)
    wb, hb, pb = pixels(b)
    if (wa, ha) != (wb, hb):
        print(f"DIM {wa}x{ha} vs {wb}x{hb}")
        return 2
    diff = 0
    minx, miny, maxx, maxy = wa, ha, -1, -1
    for y in range(ha):
        row = y * wa * 3
        for x in range(wa):
            o = row + x * 3
            if pa[o:o + 3] != pb[o:o + 3]:
                diff += 1
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    bbox = f"bbox=({minx},{miny})-({maxx},{maxy})" if diff else ""
    print(f"diffPixels={diff} {bbox}")
    return 0 if diff <= threshold else 1


if __name__ == '__main__':
    sys.exit(main())
