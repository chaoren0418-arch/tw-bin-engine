"""Read-only: render <surf>.img from the client's Sprite*.pak to PNG files in a scratch dir (Issue #429).

.img layout observed in this client: u16 width, u16 height, u16 flag, u16 color key, then width*height
RGB555 little-endian pixels (size check: 8 + w*h*2 == entry size for every file sampled).
Output PNGs are evidence for local viewing only and must stay in scratch/ (never commit game art).
Usage: py -3 -I ui_img_render.py <out dir in scratch/> <surf id> [...]
Requires Pillow.
"""
import glob
import os
import struct
import sys

from PIL import Image

ROOT = r"D:\lineage開發\0-客戶端"


def main():
    out, want = sys.argv[1], {w.lower() for w in sys.argv[2:]}
    os.makedirs(out, exist_ok=True)
    for idx in sorted(glob.glob(os.path.join(ROOT, "Sprite*.idx"))):
        b = open(idx, "rb").read()
        with open(idx[:-4] + ".pak", "rb") as pak:
            for i in range(4, len(b) - 27, 28):
                off, = struct.unpack_from("<I", b, i)
                name = b[i + 4:i + 24].split(b"\0")[0].decode("latin1")
                size, = struct.unpack_from("<I", b, i + 24)
                if not name.lower().endswith(".img") or name[:-4].lower() not in want:
                    continue
                pak.seek(off)
                data = pak.read(size)
                w, h, flag, key = struct.unpack_from("<4H", data)
                img = Image.new("RGB", (w, h))
                px = img.load()
                for y in range(h):
                    for x in range(w):
                        v, = struct.unpack_from("<H", data, 8 + (y * w + x) * 2)
                        px[x, y] = (((v >> 10) & 31) << 3, ((v >> 5) & 31) << 3, (v & 31) << 3)
                img.save(os.path.join(out, name[:-4] + ".png"))
                print(name, w, h, flag, hex(key))


if __name__ == "__main__":
    main()
