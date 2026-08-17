# -*- coding: utf-8 -*-
"""Minimal PSD parser: prints header info and layer records (name, rect, visibility)."""
import struct
import sys

def read_psd_layers(path):
    with open(path, 'rb') as f:
        data = f.read()

    sig = data[0:4]
    version = struct.unpack('>H', data[4:6])[0]
    channels = struct.unpack('>H', data[12:14])[0]
    height = struct.unpack('>I', data[14:18])[0]
    width = struct.unpack('>I', data[18:22])[0]
    depth = struct.unpack('>H', data[22:24])[0]
    color_mode = struct.unpack('>H', data[24:26])[0]
    print(f"sig={sig} version={version} size={width}x{height} depth={depth} colorMode={color_mode}")

    pos = 26
    # color mode data
    (l,) = struct.unpack('>I', data[pos:pos+4]); pos += 4 + l
    # image resources
    (l,) = struct.unpack('>I', data[pos:pos+4]); pos += 4 + l
    # layer and mask info
    (l,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
    layer_info_end = pos + l
    print(f"layer&mask section at {pos}, length {l}")

    (layer_info_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
    layer_info_end2 = pos + layer_info_len
    (layer_count,) = struct.unpack('>h', data[pos:pos+2]); pos += 2
    n = abs(layer_count)
    print(f"layer_count={layer_count} (abs {n})")

    for i in range(n):
        rect = struct.unpack('>4i', data[pos:pos+16]); pos += 16
        (nch,) = struct.unpack('>H', data[pos:pos+2]); pos += 2
        pos += nch * 6  # channel info records (id 2 bytes + length 4 bytes)
        pos += 4  # blend mode signature
        pos += 4  # blend mode key
        pos += 1  # opacity
        (clipping,) = struct.unpack('>B', data[pos:pos+1]); pos += 1
        (flags,) = struct.unpack('>B', data[pos:pos+1]); pos += 1
        pos += 1  # filler
        (extra_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
        extra_end = pos + extra_len
        # mask data
        (mask_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
        pos += mask_len
        # blending ranges
        (blend_len,) = struct.unpack('>I', data[pos:pos+4]); pos += 4
        pos += blend_len
        # pascal string name
        (name_len,) = struct.unpack('>B', data[pos:pos+1]); pos += 1
        name = data[pos:pos+name_len].decode('utf-8', errors='replace')
        print(f"    raw name bytes: {data[pos:pos+name_len].hex()}")
        pos += name_len
        # pad to even
        if (name_len + 1) % 2 == 1:
            pos += 1
        # additional layer info keys (may contain luni = unicode name)
        while pos + 12 <= extra_end:
            (klen,) = struct.unpack('>I', data[pos:pos+4])
            key = data[pos+4:pos+8].decode('latin-1')
            if key == 'luni' and pos + 12 + klen <= len(data):
                luni = data[pos+12:pos+12+klen]
                uname = luni.decode('utf-16-be', errors='replace').rstrip('\x00')
                if uname:
                    name = uname
            pos += 12 + klen
            if klen % 2:
                pos += 1
        pos = extra_end  # skip the rest of extra data (layer info keys)
        visible = (flags & 0x02) == 0
        print(f"layer[{i}] name={name!r} rect(top,left,bottom,right)={rect} ch={nch} visible={visible}")

    # optionally: total channel data
    print(f"layer info end = {layer_info_end2}, actual pos={pos}")

if __name__ == '__main__':
    # 默认 PSD 位于本脚本所在目录的上级（OctopusPet\assets\Octopus.psd）
    _default_psd = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                                'assets', 'Octopus.psd')
    read_psd_layers(sys.argv[1] if len(sys.argv) > 1 else _default_psd)
