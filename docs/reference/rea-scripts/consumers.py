"""Full-.text skipdata sweep for 36-byte frame field reads, plus TW13081901 parser/selector constant cross-check."""
import collections, re, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

im = Img()
v, t = im.text()
md = Cs(CS_ARCH_X86, CS_MODE_32)
md.skipdata = True
pat = re.compile(r"byte ptr \[e\w\w \+ e\w\w \+ 0x(14|16)\]")
window = collections.deque(maxlen=8)
hits = []
last = 0
for a, s, m, o in md.disasm_lite(t, v):
    window.append((a, m, o))
    if m in ("movzx", "movsx", "test", "cmp") and pat.search(o):
        hits.append((a, m, o, any("0x24" in w[2] for w in window)))
    last = a
print("swept to", hex(last), "hits", len(hits))
near = [h for h in hits if h[3]]
print("with imul 0x24 nearby", len(near))
for h in near:
    print(f"  {h[0]:08X} {h[1]} {h[2]}")

print("== TW13081901")
TW = r"D:\lineage開發\登入器bin資源\TW13081901bin拆解\TW13081901.unpacked.exe"
x = Img(TW, "653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4")
md2 = Cs(CS_ARCH_X86, CS_MODE_32)
md2.skipdata = True


def scan(start, size, want):
    b = x.read(start, size)
    res = collections.Counter()
    ex = {}
    for a, s, m, o in md2.disasm_lite(b, start):
        for name, rx in want.items():
            if re.search(rx, f"{m} {o}"):
                res[name] += 1
                ex.setdefault(name, f"{a:08X} {m} {o}")
    return res, ex


P = {"cmp<": r"cmp .*, 0x3c$", "cmp[": r"cmp .*, 0x5b$", "cmp^": r"cmp .*, 0x5e$", "cmp!": r"cmp .*, 0x21$",
     "cmp>": r"cmp .*, 0x3e$", "cmp]": r"cmp .*, 0x5d$", "cmp{": r"cmp .*, 0x7b$", "cmp}": r"cmp .*, 0x7d$",
     "cmp=": r"cmp .*, 0x3d$", "xor1951": r"xor .*0x1951", "xor2b": r"xor .*0x2b$",
     "set+14": r"mov byte ptr \[.*\+ 0x14\], (0|1|2)$", "or+16": r"or byte ptr|or .*, (1|2|4|8|0x10)$",
     "imul24": r"imul .*0x24", "imul3c8": r"imul .*0x3c8"}
r, e = scan(0x4E42D0, 0x2C00, P)
print(" parser 4E42D0", dict(r))
for k, s in e.items():
    print("   ", k, s)
S = {"rand": r"call dword ptr", "cmp5": r"cmp .*, 5$", "cmp2": r"cmp .*, 2$", "imul24": r"imul .*0x24", "abs+14": r"\+ 0x14\]", "abs+c": r"\+ 0xc\]"}
r, e = scan(0x5A9EE0, 0xE80, S)
print(" selector 5A9EE0", dict(r))
for k, s in e.items():
    print("   ", k, s)
