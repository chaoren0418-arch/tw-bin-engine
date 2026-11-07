
for v in (0xE53160, 0xE532A0, 0xE53310, 0xE53590, 0xE536B0, 0xE572C0, 0x9708B0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:30])
# scan for functions reading word [r+4] and word [r+6] with cmp/test and lea [r+8]
import ida_ua
hits = []
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 6000: continue
    w4 = w6 = l8 = False
    for ea in idautils.FuncItems(fs):
        m = idc.print_insn_mnem(ea)
        d = idc.GetDisasm(ea)
        if "word ptr [" in d and ("+4]" in d) and m in ("movsx", "movzx", "cmp", "test"): w4 = True
        if "word ptr [" in d and ("+6]" in d) and m in ("movsx", "movzx", "mov"): w6 = True
        if m in ("lea", "add") and ("+8]" in d or ", 8" in d): l8 = True
    if w4 and w6 and l8:
        hits.append(fs)
P("SCAN", len(hits), [hex(h) for h in hits[:300]])
