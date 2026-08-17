# -*- coding: utf-8 -*-
"""Extract each layer of a PSD as a standalone transparent PNG (cropped to its rect)."""
import struct, sys, os
from zlib import compress

def decode_packbits(data, expected):
    out = bytearray()
    i = 0
    n = len(data)
    while i < n and len(out) < expected:
        h = data[i]; i += 1
        if h < 128:
            cnt = h + 1
            out += data[i:i+cnt]
            i += cnt
        elif h > 128:
            cnt = 257 - h
            out += data[i:i+1] * cnt
            i += 1
        else:
            pass  # noop
    return bytes(out[:expected])

def extract(path, outdir):
    with open(path, 'rb') as f:
        data = f.read()
    version = struct.unpack('>H', data[4:6])[0]
    channels = struct.unpack('>H', data[12:14])[0]
    height = struct.unpack('>I', data[14:18])[0]
    width = struct.unpack('>I', data[18:22])[0]
    depth = struct.unpack('>H', data[22:24])[0]
    color_mode = struct.unpack('>H', data[24:26])[0]
    print(f"PSD v{version} {width}x{height} depth={depth} mode={color_mode}")

    pos = 26
    (l,) = struct.unpack('>I', data[pos:pos+4]); pos += 4 + l
    (l,) = struct.unpack('>I', data[pos:pos+4]); pos += 4 + l
    (lm_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
    lm_end = pos + lm_len

    (layer_info_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
    layer_info_end = pos + layer_info_len
    (layer_count,) = struct.unpack('>h', data[pos:pos+2]); pos += 2
    n = abs(layer_count)
    print(f"layers: {n}")

    layers = []
    for i in range(n):
        rect = struct.unpack('>4i', data[pos:pos+16]); pos += 16
        (nch,) = struct.unpack('>H', data[pos:pos+2]); pos += 2
        ch_info = []
        for _ in range(nch):
            (cid,) = struct.unpack('>h', data[pos:pos+2])
            (clen,) = struct.unpack('>I', data[pos+2:pos+6])
            ch_info.append((cid, clen))
            pos += 6
        pos += 4  # blend sig
        pos += 4  # blend key
        pos += 1  # opacity
        pos += 1  # clipping
        pos += 1  # flags
        pos += 1  # filler
        (extra_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
        extra_end = pos + extra_len
        (mask_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
        pos += mask_len
        (blend_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
        pos += blend_len
        (name_len,) = struct.unpack('>B', data[pos:pos+1]); pos += 1
        raw = data[pos:pos+name_len]
        try:
            name = raw.decode('gbk')
        except Exception:
            name = raw.decode('utf-8', errors='replace')
        pos += name_len
        if (name_len + 1) % 2 == 1:
            pos += 1
        pos = extra_end
        layers.append({'name': name, 'rect': rect, 'ch': ch_info})
        print(f"  layer {i}: {name!r} rect={rect}")

    # channel image data follows the layer records (pos is right after records)
    layer_w = width
    layer_h = height
    for i, ly in enumerate(layers):
        top, left, bottom, right = ly['rect']
        h = bottom - top
        w = right - left
        if w <= 0 or h <= 0:
            # group marker: still must consume its channel data (empty), keep pos in sync
            for (cid, clen) in ly['ch']:
                if pos + 2 > len(data):
                    break
                (comp,) = struct.unpack('>H', data[pos:pos+2]); pos += 2
                if comp == 1:
                    # 0 rows -> 0 row lengths; then (usually) 0 data bytes
                    total = struct.unpack('>H', data[pos:pos+2])[0] if h == 0 else 0
                    pos += total if h == 0 else h * 2
                elif comp == 0:
                    pos += w * h
            print(f"  skip {i}: {ly['name']!r} (group marker, {w}x{h})")
            continue
        rle_rows = {}
        planes = {}
        for (cid, clen) in ly['ch']:
            if pos + 2 > len(data):
                break
            (comp,) = struct.unpack('>H', data[pos:pos+2]); pos += 2
            if comp == 1:
                pos += h * 2  # row lengths
                if cid not in planes:
                    row_lens = struct.unpack('>' + 'H'*h, data[pos-h*2:pos])
                    total = sum(row_lens)
                    raw = data[pos:pos+total]
                    pos += total
                    planes[cid] = decode_packbits(raw, w*h)
                else:
                    # skip bytes: recompute
                    pass
            elif comp == 0:
                raw = data[pos:pos+w*h]
                pos += w*h
                planes[cid] = raw
            else:
                print(f"    unsupported compression {comp} for layer {i} channel {cid}")
                break
        # build RGBA via extended slice assignment (C-speed interleave)
        zeros = b'\x00' * (w*h)
        ones = b'\xff' * (w*h)
        img = bytearray(w*h*4)
        img[0::4] = planes.get(0, zeros)
        img[1::4] = planes.get(1, zeros)
        img[2::4] = planes.get(2, zeros)
        img[3::4] = planes.get(-1, ones)
        # save as PNG manually (RGBA, no interlace)
        os.makedirs(outdir, exist_ok=True)
        outpath = os.path.join(outdir, f"{i:02d}_{safe_name(ly['name'])}.png")
        write_png(outpath, w, h, bytes(img))
        print(f"  -> {outpath} ({w}x{h})")

def safe_name(n):
    import re
    return re.sub(r'[^\w\u4e00-\u9fff]+', '_', n)

def write_png(path, w, h, rgba):
    def chunk(tag, payload):
        c = struct.pack('>I', len(payload)) + tag + payload
        crc = 0
        crc ^= 0xFFFFFFFF
        for b in tag + payload:
            crc ^= b
            for _ in range(8):
                crc = (crc >> 1) ^ (0xEDB88320 if crc & 1 else 0)
        crc ^= 0xFFFFFFFF
        return c + struct.pack('>I', crc)
    sig = b'\x89PNG\r\n\x1a\n'
    ihdr = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)
    raw = bytearray()
    stride = w * 4
    for y in range(h):
        raw.append(0)
        raw += rgba[y*stride:(y+1)*stride]
    idat = compress(bytes(raw), 6)
    with open(path, 'wb') as f:
        f.write(sig)
        f.write(chunk(b'IHDR', ihdr))
        f.write(chunk(b'IDAT', idat))
        f.write(chunk(b'IEND', b''))

if __name__ == '__main__':
    _dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'assets')
    _psd = os.path.join(_dir, 'Octopus.psd')
    _out = os.path.join(_dir, 'layers')
    extract(sys.argv[1] if len(sys.argv) > 1 else _psd,
            sys.argv[2] if len(sys.argv) > 2 else _out)
