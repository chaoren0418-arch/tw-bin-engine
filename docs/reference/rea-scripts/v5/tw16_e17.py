P("A61DE0 callers", [(hex(a), b and hex(b)) for a,b,t in xr(0xA61DE0)])
P("7DA7E0 MacroWindow ctor callers", [(hex(a), b and hex(b)) for a,b,t in xr(0x7DA7E0)])
for v in (0x707210, 0x7074B0, 0xADA900):
    t = dec(v); P(hex(v), (t or "").count("\n"))
hits=[]
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 60000: continue
    seen=set()
    for ea in idautils.FuncItems(fs):
        m = idc.print_insn_mnem(ea)
        if m in ("cmp","sub","add") and idc.get_operand_type(ea,1)==idc.o_imm:
            v = idc.get_operand_value(ea,1) & 0xffffffff
            if 0x70 <= v <= 0x7B: seen.add(v)
    if len(seen) >= 5: hits.append((fs, sorted(seen)))
for fs, s in hits: P("VK", hex(fs), ida_funcs.get_func(fs).size(), [hex(x) for x in s])