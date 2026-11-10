"""T304 (#716): read-only linear sweep of TW16 .text for E8 rel32 calls and imm32 operands that hit the given VAs.
Catches call sites IDA left outside functions (used to show WorldMapWindow/VillageMapWindow ctors have no caller).
Usage: py -3 -I -X utf8 t716_callscan.py A64460 A63A30 153F2D8"""
import sys, struct, pathlib
here = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(next(p for p in (here, *here.parents) if (p / "nat.py").exists())))
from nat import Img

im = Img()
v, t = im.text()
for a in sys.argv[1:]:
    T = int(a, 16)
    for i in range(1, len(t) - 5):
        if t[i] == 0xE8 and (v + i + 5 + struct.unpack_from("<i", t, i + 1)[0]) & 0xFFFFFFFF == T:
            print(a, "call", hex(v + i), "FO", hex(v + i - 0x400C00), "func~", hex(im.func_start(v + i) or 0))
        elif struct.unpack_from("<I", t, i)[0] == T:
            print(a, "imm", hex(v + i), "FO", hex(v + i - 0x400C00))
