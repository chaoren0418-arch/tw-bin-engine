"""V3 (#458): for TW16 opcodes, print the sub_947530 case block and the handler it calls. Read-only; uses nat.py (BIN path/SHA pinned there).
Usage: py -3 -I docs/reference/rea-scripts/buff_handlers.py 93 92 69 4 196 17 160 33 108"""
import sys, struct, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
names = {}
for op in range(256):
    idx = im.read(0x951E78 + op, 1)[0]
    tgt = im.d32(0x951C5C + 4 * idx)
    ins = im.read(tgt, 7)
    if ins and ins[:3] == b"\xc7\x45\xfc":
        names[op] = im.read(struct.unpack("<I", ins[3:7])[0], 64).split(b"\0")[0].decode("latin1")
cases = {}
for op in range(256):
    idx = im.read(0x94828C + op, 1)[0]
    if idx > 114: continue
    cases.setdefault(im.d32(0x9480C0 + 4 * idx), []).append(op)
want = [int(a) for a in sys.argv[1:]] or sorted(names)
for op in want:
    idx = im.read(0x94828C + op, 1)[0]
    if idx > 114:
        print(op, names.get(op), "no case"); continue
    blk = im.d32(0x9480C0 + 4 * idx)
    code = im.read(blk, 0x80)
    calls = []
    for i in im.md.disasm(code, blk):
        if i.mnemonic == "call": calls.append(i.op_str)
        if i.mnemonic in ("jmp", "ret"): break
    print(op, names.get(op), hex(blk), "shared" if len(cases[blk]) > 1 else "", calls)
