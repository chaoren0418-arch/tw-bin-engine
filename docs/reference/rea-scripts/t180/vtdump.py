"""T180 (#482): ARG = 'vt1,vt2,...' (hex). For each vtable list up to 30 code pointers; decompile entries that are
not shared with the first vtable (base) plus the base's own entries. Output vt.json + *.c."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import ida_bytes, ida_funcs

def entries(vt):
    out = []
    for i in range(30):
        p = ida_bytes.get_dword(vt + 4 * i)
        if not (0x401000 <= p < 0x1000000) or not ida_funcs.get_func(p):
            break
        out.append(p)
    return out

def main(rep):
    import idc
    vts = [idc.get_name_ea_simple(x) if x.startswith("??") else int(x, 16) for x in ARG.split(",")]
    base = entries(vts[0])
    res = {}
    for vt in vts:
        e = entries(vt)
        res[hex(vt)] = [hex(x) for x in e]
        for i, p in enumerate(e):
            if vt == vts[0] or i >= len(base) or base[i] != p:
                dump(p, "_vt%X_%d" % (vt, i))
    (OUT / "vt.json").write_text(json.dumps(res, indent=1), encoding="utf-8")
run(main)
