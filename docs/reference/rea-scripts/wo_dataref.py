"""T182 (#484): find 4-byte references to VAs in every section and dump neighbours (vtables, event tables). Read-only; uses nat.py.
Usage: py -3 -I wo_dataref.py 0x582C20"""
import sys, pathlib, struct
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
for a in sys.argv[1:]:
    w = int(a, 16); pat = struct.pack("<I", w); i = 0
    while True:
        i = im.data.find(pat, i)
        if i < 0: break
        # map offset to va
        for n, v, vs, p, r in im.secs:
            if p <= i < p + r:
                va = v + i - p
                ctx = [hex(im.d32(va + k)) for k in range(-16, 20, 4)]
                strs = []
                for k in range(-16, 20, 4):
                    d = im.d32(va + k); b = im.read(d, 40) if d else None
                    if b and 32 <= b[0] < 127: strs.append((hex(d), b.split(b"\0")[0][:40]))
                print(a, n, hex(va), ctx, strs)
        i += 1
