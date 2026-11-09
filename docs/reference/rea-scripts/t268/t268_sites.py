"""T268 (#629): list sub_9FBC00 call sites in [lo,hi) with preceding instructions. Usage: py -3 -I t268_sites.py LO HI"""
import sys, pathlib, struct
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img(); v, t = im.text(); md = im.md
lo, hi = int(sys.argv[1], 16), int(sys.argv[2], 16)
T = 0x9FBC00
for i in range(len(t) - 5):
    if t[i] == 0xE8 and (v + i + 5 + struct.unpack_from("<i", t, i + 1)[0]) & 0xFFFFFFFF == T:
        site = v + i
        if not (lo <= site < hi): continue
        ins = list(md.disasm(t[i - 60:i], site - 60))
        print(hex(site), " | ".join(f"{x.mnemonic} {x.op_str}" for x in ins[-7:]))
