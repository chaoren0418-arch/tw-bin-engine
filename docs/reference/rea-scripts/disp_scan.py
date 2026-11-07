"""List [reg+disp32] uses of one displacement in .text (raw modrm check, then capstone verify)."""
import struct, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt

im = Img()
v, t = im.text()
disp = int(sys.argv[1], 16)
pat = struct.pack("<I", disp)
OPS = {0x8B, 0x89, 0x83, 0x81, 0x39, 0x3B, 0xC7, 0x8D, 0xFF}
i, seen = 0, {}
while True:
    i = t.find(pat, i)
    if i < 0:
        break
    m = t[i - 1]
    if (m & 0xC0) == 0x80 and (m & 7) != 4 and t[i - 2] in OPS:
        ins = list(im.md.disasm(t[i - 2:i + 13], v + i - 2, 1))
        if ins and hex(disp) in ins[0].op_str:
            f = im.func_start(v + i - 2) or 0
            seen.setdefault(f, []).append(fmt(ins[0]))
    i += 1
print("functions", len(seen), "uses", sum(map(len, seen.values())))
for f, rows in sorted(seen.items()):
    print(f"{f:08X}")
    for r in rows:
        print("   ", r)
