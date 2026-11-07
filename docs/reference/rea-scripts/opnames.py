"""TW16 server-opcode name table (sub_9515E0): opcode -> S_/C_ name string. Read-only."""
import sys, struct, pathlib, json
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
def cstr(va):
    b = im.read(va, 64); return b.split(b"\0")[0].decode("latin1")
out = {}
for op in range(255):
    idx = im.read(0x951E78 + op, 1)[0]
    tgt = im.d32(0x951C5C + 4 * idx)
    ins = im.read(tgt, 7)
    if ins[:3] == b"\xc7\x45\xfc":
        out[op] = cstr(struct.unpack("<I", ins[3:7])[0])
want = sys.argv[1:] 
for op, n in out.items():
    if not want or any(w in n for w in want):
        print(op, n)
