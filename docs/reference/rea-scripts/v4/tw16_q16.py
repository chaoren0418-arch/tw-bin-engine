
import re
def vt_of(name_bytes):
    for s in find_all(name_bytes, 3):
        td = s - 8
        for ea in find_all(td.to_bytes(4,"little"), 40):
            col = ea - 12
            if ida_bytes.get_dword(col) != 0: continue
            for v in find_all(col.to_bytes(4,"little"), 10):
                yield v + 4
seen = set()
for nm in (b".?AUStatus800@@\x00", b".?AVStatus800@@\x00", b".?AUStatusBase@@\x00"):
    for vt in vt_of(nm):
        fns = []
        for i in range(60):
            f = ida_bytes.get_dword(vt+4*i)
            g = ida_funcs.get_func(f)
            if not g or g.start_ea != f: break
            fns.append(f)
        P("VT", nm, hex(vt), [hex(f) for f in fns])
        for f in fns:
            if f in seen: continue
            seen.add(f)
            t = dec(f) or ""
            hits = [l.strip() for l in t.split("\n") if re.search(r"this\[(22|23|33|34|35|36|37|38)\]|\+ 88\)|\+ 92\)", l)]
            if hits: P("  USE", hex(f), hits[:12])
# A83CD0 = element ctor (surf,x,y); fields
dec(0xA83CD0)
