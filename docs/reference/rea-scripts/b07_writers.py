"""Classify absolute operand refs by raw opcode bytes, then show writer context."""
import struct, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt

im = Img()
v, t = im.text()
MOD = {0x05, 0x0D, 0x15, 0x1D, 0x25, 0x2D, 0x35, 0x3D}


def classify(i):
    p1, p2 = t[i - 1], t[i - 2]
    if p1 == 0xA3:
        return "W", i - 1
    if p1 == 0xA1:
        return "R", i - 1
    if p1 in MOD:
        if p2 == 0x89:
            return "W", i - 2
        if p2 == 0xC7:
            return "W", i - 2
        if p2 == 0x8B:
            return "R", i - 2
        if p2 in (0x83, 0x81, 0x39, 0x3B, 0x85, 0x80, 0x8D, 0xFF, 0x01, 0x03, 0x29, 0x2B, 0x31, 0x33, 0xF7, 0xD9):
            return f"op{p2:02x}", i - 2
    if p1 in (0x68,):
        return "PUSHIMM", i - 1
    if p1 in (0xB8, 0xB9, 0xBA, 0xBB, 0xBE, 0xBF):
        return "MOVIMM", i - 1
    return "?", i


for target in [int(a, 16) for a in sys.argv[1:]]:
    pat = struct.pack("<I", target)
    i = 0
    rows = []
    while True:
        i = t.find(pat, i)
        if i < 0:
            break
        rows.append((classify(i), v + i))
        i += 1
    print(f"== {target:08X} sec={im.sec(target)} refs={len(rows)}")
    for (k, s), at in rows:
        ins = list(im.md.disasm(t[s:s + 15], v + s, 1))
        print(f"  {k:8}", fmt(ins[0]) if ins else hex(at))
    for (k, s), at in rows:
        if k == "W":
            f = im.func_start(v + s)
            print(f"  -- writer ctx {v+s:08X} func~{f and hex(f)}")
            start = max(f or 0, v + s - 0x50)
            for ins in im.dis(f if f and v + s - f < 0x60 else v + s - 0x40, 0x90):
                if ins.address >= v + s - 0x38:
                    print("     ", fmt(ins))
                if ins.address > v + s + 0x10:
                    break
