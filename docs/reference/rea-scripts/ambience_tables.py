"""Dump on-disk .data tables used by the darkness / tint paths (read-only)."""
import sys, struct, pathlib, json
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
print("sec", im.sec(0x1374168), im.sec(0x1369D50))
t = im.read(0x1374168, 3 * 512)
for mode in range(3):
    print("mode", mode)
    for lv in range(16):
        row = t[512 * mode + 32 * lv: 512 * mode + 32 * lv + 32]
        print(f" L{lv:2d}", " ".join(f"{b:2d}" for b in row))
print("1369D50", hex(im.d32(0x1369D50)))
print("1369D70", [hex(im.d32(0x1369D70 + 4 * i)) for i in range(24)])
print("1374160", im.d32(0x1374160))
print("1379D10", list(im.read(0x1379D10, 64)))
