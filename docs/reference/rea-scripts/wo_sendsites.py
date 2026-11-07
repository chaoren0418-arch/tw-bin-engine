"""T182 (#484): list sub_9FBC00 send sites whose opcode push matches. Read-only; uses nat.py.
Usage: py -3 -I wo_sendsites.py 23 131 179"""
import sys, pathlib, re
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt
im = Img()
v, t = im.text()
want = set(int(a) for a in sys.argv[1:])
md = im.md
# linear sweep for E8 rel32 to 0x9FBC00
import struct
T = 0x9FBC00
for i in range(len(t) - 5):
    if t[i] == 0xE8 and (v + i + 5 + struct.unpack_from("<i", t, i + 1)[0]) & 0xFFFFFFFF == T:
        site = v + i
        win = t[i - 40:i]
        ins = list(md.disasm(win, site - 40))
        pushes = [x for x in ins if x.mnemonic == "push"]
        # fmt pushed last, opcode pushed second-to-last
        if len(pushes) >= 2:
            op = pushes[-2].op_str
            try: val = int(op, 16)
            except ValueError: continue
            if val in want:
                print(hex(site), "opcode", val, "fmt", pushes[-1].op_str, "func~", hex(im.func_start(site) or 0))
