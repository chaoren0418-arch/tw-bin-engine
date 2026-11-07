"""T182 (#484): TW16 send-opcode -> C_* name (sub_951F80: opcode-3 -> index 0x952870 -> jump 0x952640). Read-only; uses nat.py.
Usage: py -3 -I wo_copnames.py [opcode ...]"""
import sys, pathlib, struct, re
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt
im = Img()
va = 0xd84710 + 0x401400
x = im.xrefs(va)
print([ (hex(a), fmt(i) if i else None) for a,i in x][:3])
site = x[0][1].address
fs = im.func_start(site); print("func", hex(fs))
idx = jt = None
for i in im.dis(fs, 0x200):
    s = i.op_str
    m = re.search(r"movzx \w+, byte ptr \[\w+ \+ (0x[0-9a-f]+)\]", i.mnemonic + " " + s)
    if m and idx is None: idx = int(m.group(1), 16); print(fmt(i))
    m = re.search(r"dword ptr \[\w+\*4 \+ (0x[0-9a-f]+)\]", s)
    if i.mnemonic == "jmp" and m: jt = int(m.group(1), 16); print(fmt(i)); break
names = {}
for op in range(3, 3 + 0xfc):
    k = im.read(idx + op - 3, 1)[0]  # sub ecx,3 at 0x951F97
    tgt = im.d32(jt + 4 * k)
    if tgt is None or im.sec(tgt)[0] != ".text": continue
    for ins in im.dis(tgt, 16, 2):
        mm = re.search(r"(0x1[0-9a-f]{6})", ins.op_str)
        if ins.mnemonic in ("mov","push") and mm:
            b = im.read(int(mm.group(1), 16), 48)
            if b and b.startswith(b"C_"):
                names[op] = b.split(b"\0")[0].decode(); break
for op, n in sorted(names.items()):
    if not sys.argv[1:] or op in map(int, sys.argv[1:]): print(op, n)
