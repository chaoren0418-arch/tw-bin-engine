"""Cross-check parser constants in the fully unpacked TW13081901 image (function at the list.spr xref)."""
import collections, re, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

x = Img(r"D:\lineage開發\登入器bin資源\TW13081901bin拆解\TW13081901.unpacked.exe",
        "653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4")
md = Cs(CS_ARCH_X86, CS_MODE_32)
md.skipdata = True
start, size = 0x4A34F0, 0x3000
want = {"cmp<": 0x3C, "cmp[": 0x5B, "cmp^": 0x5E, "cmp!": 0x21, "cmp>": 0x3E, "cmp]": 0x5D, "cmp{": 0x7B, "cmp}": 0x7D, "cmp=": 0x3D, "cmp-": 0x2D}
res, first, setmode, orflag, xors = collections.Counter(), {}, [], [], []
end = None
for a, s, m, o in md.disasm_lite(x.read(start, size), start):
    if m == "cmp":
        for k, val in want.items():
            if o.endswith(f", {hex(val)}"):
                res[k] += 1
                first.setdefault(k, hex(a))
    if m == "mov" and re.search(r"byte ptr \[.* \+ 0x14\], [012]$", o):
        setmode.append((hex(a), o))
    if m == "or" and re.search(r"(, 1|, 2|, 4|, 8|, 0x10)$", o):
        orflag.append((hex(a), o))
    if m == "xor" and "0x" in o:
        xors.append((hex(a), o))
    if m == "imul" and ("0x24" in o or "0x3c8" in o):
        res[o.split(",")[-1].strip()] += 1
print("parser candidate", hex(start), dict(res))
print(" first", first)
print(" set +0x14", setmode[:8])
print(" or flags", orflag[:10])
print(" xor imm", xors[:10])
