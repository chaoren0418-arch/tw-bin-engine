for v in (0x792230, 0x7929B0, 0x785700, 0x4D7E50, 0x782740, 0x7859B0, 0x77F6B0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:12])
P("call site 4db4d6"); P(dis(0x4db4a0, 30))
# writers of Status800 +0x1F4 / +0x1F8 (mode / current sub)
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 80000: continue
    w = []
    for ea in idautils.FuncItems(fs):
        if idc.print_insn_mnem(ea) == "mov" and idc.get_operand_type(ea,0) == idc.o_displ:
            d = idc.get_operand_value(ea,0) & 0xffffffff
            if d in (0x1F4, 0x1F8):
                w.append((hex(ea), idc.GetDisasm(ea)))
    if w:
        rd = set()
        for ea in idautils.FuncItems(fs):
            for i in range(2):
                if idc.get_operand_type(ea,i)==idc.o_displ:
                    rd.add(idc.get_operand_value(ea,i)&0xffffffff)
        if {0x1FC, 0x200} & rd or 0x204 in rd:
            P("W1F4/1F8", hex(fs), fn.size(), w[:6])
            dec(fs)
# keyboard: teleport_F%d and VK compares
for s in (b"teleport_F%d.txt\x00",):
    for h in find_all(s, 3):
        P("str", s, hex(h), [(hex(a), b and hex(b)) for a,b,t in xr(h)])
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
    if len(seen) >= 3: hits.append((fs, sorted(seen)))
for fs, s in hits: P("VK", hex(fs), ida_funcs.get_func(fs).size(), [hex(x) for x in s], "callers", sorted({hex(b) for a,b,t in xr(fs) if b})[:6])
