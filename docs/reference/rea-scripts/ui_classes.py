"""T202 (#504): resolve UI constructor VAs to their MSVC RTTI class name (vtable[-1] -> COL -> TypeDescriptor). Read-only.

Input: a pseudocode file of sub_B56A40 (XML UI factory, from ida_dec.py) to pull `strcmp(Str1, "Name")` -> sub_XXXX pairs,
       plus RTTI names that end with Window (legacy hard-coded windows built by sub_8C7C80).
Usage: py -3 -I docs/reference/rea-scripts/ui_classes.py <00B56A40.c> [out.json]
"""
import json, re, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
sys.stdout.reconfigure(encoding="utf-8")
from nat import Img

im = Img()


def rtti_of_vtable(vt):
    col = im.d32(vt - 4)
    if not col or im.sec(col)[0] != ".rdata":
        return None
    td = im.d32(col + 12)
    if not td or im.off(td) is None:
        return None
    b = im.read(td + 8, 160)
    if not b or not b.startswith(b".?A"):
        return None
    return b.split(b"\0")[0].decode("latin1")


def ctor_class(va):
    best = None
    for ins in im.dis(va, 0x600):
        if ins.mnemonic == "ret":
            break
        m = re.fullmatch(r"dword ptr \[(e\w\w)\], (0x[0-9a-f]+)", ins.op_str)
        if ins.mnemonic == "mov" and m:
            vt = int(m.group(2), 16)
            n = rtti_of_vtable(vt)
            if n:
                best = (vt, n)  # last write wins (derived class)
    return best


src = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8")
pairs = re.findall(r'strcmp\(Str1, "([^"]+)"\) \)\s*\{(?:(?!strcmp).)*?sub_([0-9A-F]+)\(', src, re.S)
out = {"xml_ui": [], "legacy_window_rtti": []}
for name, ctor in pairs:
    va = int(ctor, 16)
    r = ctor_class(va)
    out["xml_ui"].append(dict(ui=name, ctor=f"0x{va:X}", ctor_fo=f"0x{im.off(va):X}",
                              vtable=f"0x{r[0]:X}" if r else None, rtti=r[1] if r else None))
for m in re.finditer(rb"\.\?A[UV]([A-Za-z0-9_]*(?:Window|Win|Control))@@", im.data):
    o = m.start()
    va = next(v + o - p for n, v, vs, p, r in im.secs if p <= o < p + r)
    out["legacy_window_rtti"].append(dict(name=m.group(1).decode(), va=f"0x{va:X}", fo=f"0x{o:X}"))
p = pathlib.Path(sys.argv[2] if len(sys.argv) > 2 else "scratch/t202/ui_classes.json")
p.parent.mkdir(parents=True, exist_ok=True)
p.write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding="utf-8")
for e in out["xml_ui"]:
    print(e["ui"], e["ctor"], e["ctor_fo"], e["rtti"])
print("legacy:", " ".join(e["name"] for e in out["legacy_window_rtti"]))
