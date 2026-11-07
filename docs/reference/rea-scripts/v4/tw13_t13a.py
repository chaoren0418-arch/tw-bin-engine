
import re
def strx(s):
    r = []
    for h in find_all(s, 5):
        r += [(a, b) for a, b, t in xr(h)]
    return r
g = strx(b"Surf[%d] Error.\x00")
P("SURFERR", [(hex(a), b and hex(b)) for a, b in g])
getsurf = g[0][1] if g else None
def vt_of(name_bytes):
    for s in find_all(name_bytes, 3):
        td = s - 8
        for ea in find_all(td.to_bytes(4,"little"), 40):
            col = ea - 12
            if ida_bytes.get_dword(col) != 0: continue
            for v in find_all(col.to_bytes(4,"little"), 10):
                yield v + 4
P("LEVEL_BTN", [(hex(a), b and hex(b)) for a, b in strx(b"LEVEL_BTN\x00")])
P("BALOG_BTN", [(hex(a), b and hex(b)) for a, b in strx(b"BALOG_BTN\x00")])
P("SHOUT_BTN", [(hex(a), b and hex(b)) for a, b in strx(b"SHOUT_BTN\x00")])
seen = set()
for nm in (b".?AUStatus800@@\x00",):
    for vt in vt_of(nm):
        fns = []
        for i in range(60):
            f = ida_bytes.get_dword(vt+4*i)
            gg = ida_funcs.get_func(f)
            if not gg or gg.start_ea != f: break
            fns.append(f)
        P("VT", hex(vt), [hex(f) for f in fns])
        for f in fns:
            if f in seen: continue
            seen.add(f)
            t = dec(f) or ""
            if getsurf:
                gs = "sub_%X" % getsurf
                hits = [l.strip() for l in t.split("\n") if gs in l]
                if hits: P("  GS", hex(f), hits[:20])
