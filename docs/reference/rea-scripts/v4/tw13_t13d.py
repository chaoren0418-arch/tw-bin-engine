
import re
cands = []
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 20000 or fs == 0x5FE870: continue
    d58 = d5c = False
    for ea in idautils.FuncItems(fs):
        if idc.print_insn_mnem(ea) != "mov": continue
        dd = idc.GetDisasm(ea)
        if re.search(r", \[e[a-z]{2}\+58h\]", dd): d58 = True
        if re.search(r", \[e[a-z]{2}\+5Ch\]", dd): d5c = True
    if d58 and d5c:
        cands.append(fs)
P("CANDS", len(cands))
for fs in cands[:400]:
    t = dec(fs, save=False) or ""
    L = t.split("\n")
    hits = [l.strip() for l in L if re.search(r"\+ 88\)|\[22\]", l)]
    if hits and len(hits) < 30 and ("+ 1" in t):
        dec(fs)
        P("HIT", hex(fs), len(L), hits[:6])
