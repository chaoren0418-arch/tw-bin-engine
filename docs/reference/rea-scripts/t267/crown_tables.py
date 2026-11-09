"""#628 (T267): read-only. Dump crown offset tables and the agit map surf table from the unpacked BIN (.data, raw).
Usage: py -3 -I crown_tables.py <dir containing nat.py> OUT.json"""
import json, struct, sys
sys.path.insert(0, sys.argv[1])
from nat import Img

im = Img()
out = {}
# (name, VA, frames per heading); 8 headings, (dx, dy) pairs
for name, va, frames in (("frames12", 0x138CCC8, 12), ("frames8", 0x138D560, 8), ("frames6", 0x138D158, 6), ("other", 0x138CA18, 4)):
    n = 8 * frames * 2
    vals = struct.unpack(f"<{n}i", im.read(va, 4 * n))
    out[name] = {"va": hex(va), "fo": hex(im.off(va)),
                 "rows": [[[vals[(h * frames + f) * 2], vals[(h * frames + f) * 2 + 1]] for f in range(frames)] for h in range(8)]}
va = 0x135DA34
out["agitMapSurf"] = {"va": hex(va), "fo": hex(im.off(va)), "values": list(struct.unpack("<5i", im.read(va, 20)))}
json.dump(out, open(sys.argv[2], "w"), indent=1)
for k, v in out.items():
    print(k, v.get("va"), v.get("fo"), v.get("values", ""))
    for h, row in enumerate(v.get("rows", [])):
        print("  h", h, " ".join(f"({a},{b})" for a, b in row))
