"""T182 (#484): linear scan of .text for call rel32 to the given VAs. Read-only; uses nat.py.
Usage: py -3 -I wo_callers.py 0xBA5B70"""
import sys, pathlib, struct
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt
im = Img(); v, t = im.text()
for a in sys.argv[1:]:
    T = int(a, 16)
    for i in range(len(t) - 5):
        if t[i] == 0xE8 and (v + i + 5 + struct.unpack_from("<i", t, i + 1)[0]) & 0xFFFFFFFF == T:
            print(a, "call at", hex(v + i), "func~", hex(im.func_start(v + i) or 0))
