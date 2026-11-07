
import re
cands = []
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 4000: continue
    f4 = f6 = False
    for ea in idautils.FuncItems(fs):
        d = idc.GetDisasm(ea)
        if "word ptr [" in d:
            if re.search(r"word ptr \[e[a-z]{2}\+4\]", d): f4 = True
            if re.search(r"word ptr \[e[a-z]{2}\+6\]", d): f6 = True
    if f4 and f6:
        cands.append(fs)
P("CANDS", len(cands))
good = []
for fs in cands:
    t = dec(fs, save=False) or ""
    # __int16* param p with p[2] and p[3] and p + 4 (pixels)
    for m in re.finditer(r"__int16 \*(\w+)", t):
        v = m.group(1)
        if f"{v}[2]" in t and f"{v}[3]" in t and (f"{v} + 4" in t or f"{v}[4]" in t) and (f"{v}[1]" in t) and (f"*{v}" in t):
            good.append((fs, v)); break
P("GOOD", len(good), [(hex(a), v) for a, v in good])
for a, v in good[:60]:
    dec(a)
    P("  ", hex(a), "callers", len({b for x,b,t in xr(a) if b}), sorted({hex(b) for x,b,t in xr(a) if b})[:8])
