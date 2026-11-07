"""Read-only: print values this V1 report reads straight from TW1608262200.bin (.data / .rdata).
Usage: py -3 -I hpmp_bin_values.py <TW1608262200.bin>
"""
import struct, sys
d = open(sys.argv[1], "rb").read()
DATA = 0x401800  # .data: FO = VA - 0x401800 (initialised part)
def fo(va): return va - DATA
for name, va in (("shape dword_138B620", 0x138B620), ("status dword_138BFAC", 0x138BFAC)):
    print(name, struct.unpack_from("<i", d, fo(va))[0])
for name, va in (("showCharacterHPMP byte_138BE7C", 0x138BE7C), ("FloatingHPMP.show byte_138BE7D", 0x138BE7D)):
    print(name, d[fo(va)])
print("HP warn ratio (class 7) dword_138B62C", struct.unpack_from("<f", d, fo(0x138B62C))[0])
for va in (0x138B630, 0x138B6F0):
    print("clock table", hex(va), [struct.unpack_from("<4i", d, fo(va) + 16 * i) for i in range(12)])
for i in (0, 7, 9, 14, 33, 53):
    v = struct.unpack_from("<I", d, fo(0x1369D70) + 4 * i)[0]
    r, g, b = (v >> 10) & 31, (v >> 5) & 31, v & 31
    print("palette", i, hex(v), "#%02X%02X%02X" % tuple(round(c * 255 / 31) for c in (r, g, b)))
