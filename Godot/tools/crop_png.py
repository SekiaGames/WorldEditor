"""裁剪并放大 PNG（纯标准库，不依赖 Pillow）。用于核对 1px 边框这类界面细节。

用法：
    python tools/crop_png.py 输入.png 输出.png x y 宽 高 放大倍数
例：
    python tools/crop_png.py shot.png corner.png 6 126 40 30 10
"""

import sys, zlib, struct

def read_png(path):
    d = open(path,'rb').read()
    assert d[:8] == b'\x89PNG\r\n\x1a\n'
    pos = 8; idat = b''; w=h=bd=ct=None
    while pos < len(d):
        ln = struct.unpack('>I', d[pos:pos+4])[0]; typ = d[pos+4:pos+8]
        data = d[pos+8:pos+8+ln]; pos += 12+ln
        if typ == b'IHDR':
            w,h,bd,ct,_,_,_ = struct.unpack('>IIBBBBB', data)
        elif typ == b'IDAT':
            idat += data
        elif typ == b'IEND':
            break
    raw = zlib.decompress(idat)
    ch = {0:1,2:3,3:1,4:2,6:4}[ct]
    stride = w*ch
    out = bytearray(h*stride)
    prev = bytearray(stride)
    p = 0
    for y in range(h):
        f = raw[p]; p += 1
        line = bytearray(raw[p:p+stride]); p += stride
        if f == 1:
            for i in range(ch, stride): line[i] = (line[i] + line[i-ch]) & 0xff
        elif f == 2:
            for i in range(stride): line[i] = (line[i] + prev[i]) & 0xff
        elif f == 3:
            for i in range(stride):
                a = line[i-ch] if i >= ch else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xff
        elif f == 4:
            for i in range(stride):
                a = line[i-ch] if i >= ch else 0
                b = prev[i]; c = prev[i-ch] if i >= ch else 0
                pa = abs(b-c); pb = abs(a-c); pc = abs(a+b-2*c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xff
        out[y*stride:(y+1)*stride] = line
        prev = line
    return w,h,ch,out

def write_png(path, w, h, ch, pix, scale=1):
    rows = []
    for y in range(h):
        for _ in range(scale):
            row = bytearray()
            for x in range(w):
                px = pix[(y*w+x)*ch:(y*w+x)*ch+ch]
                if ch == 3: px = px + b'\xff'
                for _ in range(scale): row += px
            rows.append(bytes(row))
    raw = b''.join(b'\x00'+r for r in rows)
    def chunk(t,d):
        return struct.pack('>I',len(d)) + t + d + struct.pack('>I', zlib.crc32(t+d)&0xffffffff)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w*scale, h*scale, 8, 6,0,0,0)) + chunk(b'IDAT', zlib.compress(raw,9)) + chunk(b'IEND', b'')
    open(path,'wb').write(png)

src, dst, x0, y0, cw, chh, scale = sys.argv[1], sys.argv[2], *map(int, sys.argv[3:8])
w,h,ch,pix = read_png(src)
crop = bytearray()
for y in range(y0, min(y0+chh, h)):
    for x in range(x0, min(x0+cw, w)):
        crop += pix[(y*w+x)*ch:(y*w+x)*ch+ch]
write_png(dst, min(cw, w-x0), min(chh, h-y0), ch, crop, scale)
print(f"cropped {w}x{h} -> {dst}")
