"""Frame-field consumers, opcode-87 switch landing, and TW13081901 parser cross-check."""
import csv, json, pathlib, re, struct, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt

im = Img()
v, t = im.text()

print("== frame byte consumers (+0x14 mode, +0x16 flags) in A00000-A60000, E00000-F20000")
lo, hi = 0xA00000, 0xA60000
insns = list(im.md.disasm(t[lo - v:hi - v], lo))
print("decoded to", hex(insns[-1].address))
for idx, i in enumerate(insns):
    s = i.op_str
    if re.search(r"byte ptr \[.*\+ 0x1[46]\]", s) and i.mnemonic in ("movzx", "movsx", "test", "cmp", "mov"):
        prev = "; ".join(f"{x.mnemonic} {x.op_str}" for x in insns[idx - 3:idx])
        nxt = "; ".join(f"{x.mnemonic} {x.op_str}" for x in insns[idx + 1:idx + 3])
        if "imul" in prev or "0x24" in prev or "sub_" in prev or re.search(r"\[e.x \+ e.x \+ 0x1[46]\]", s):
            print(f"  {fmt(i)}   <<{prev}>>  >>{nxt}>>")

print("== switch 1 landing for byte values")
d = json.load(open(sys.argv[1], encoding="utf-8"))
sw = d["switches"]["first"]["cases"]
inv = {c: k for k, vs in sw.items() for c in vs}
for op in (8, 11, 87, 112, 158, 206, 226):
    tgt = int(inv[op], 16)
    calls = []
    for i in im.dis(tgt, 0x80):
        if i.mnemonic == "call":
            calls.append(i.op_str)
        if i.mnemonic == "jmp" or len(calls) >= 3:
            break
    print(f"  op {op:3d} (0x{op:02X}) -> {tgt:08X} calls {calls}  shared_with {len(sw[inv[op]])} values")
print("default target", inv.get(0xFE), "n cases", len(sw))

print("== who calls factory A4E5D0 / self-object A51920 from switch region")
for tgt in (0xA4E5D0, 0xA51920):
    rel_hits = []
    for a in range(0, len(t) - 5):
        if t[a] == 0xE8 and v + a + 5 + struct.unpack_from("<i", t, a + 1)[0] == tgt:
            rel_hits.append(v + a)
    print(f"  {tgt:X} direct calls {len(rel_hits)}: {[hex(x) for x in rel_hits[:30]]}")

print("== TW13081901 parser cross-check")
base = pathlib.Path(r"D:\lineage開發\登入器bin資源\TW13081901bin拆解\analysis-final")
with open(base / "strings-with-xrefs.csv", encoding="utf-8", errors="replace") as f:
    rows = list(csv.reader(f))
print("  header", rows[0])
for r in rows:
    line = ",".join(r)
    if re.search(r"loadSprites|list\.spr|LoadSprite Start|Sprite file %d", line):
        print("  ", line[:200])
