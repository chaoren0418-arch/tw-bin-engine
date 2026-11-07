"""T182 (#484): scan .text for a 32-bit immediate operand. Read-only; uses nat.py.
Usage: py -3 -I wo_immscan.py 0x29a"""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt
im = Img()
v, t = im.text()
want = [int(a,16) for a in sys.argv[1:]]
import struct
for w in want:
    pat = struct.pack("<I", w)
    i = 0; n = 0
    while True:
        i = t.find(pat, i)
        if i < 0 or n > 40: break
        for back in range(1, 8):
            ins = list(im.md.disasm(t[i-back:i-back+15], v+i-back, 1))
            if ins and ins[0].address + ins[0].size >= v + i + 4 and ins[0].address + ins[0].size <= v+i+8 and hex(w) in ins[0].op_str:
                print(hex(w), fmt(ins[0]), "func~", hex(im.func_start(ins[0].address) or 0)); n += 1; break
        i += 1
