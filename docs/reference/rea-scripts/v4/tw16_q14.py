
for v in (0x97CA60, 0x97C980, 0xE53DA0, 0x97CC40):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", len({b for a,b,tt in xr(v) if b}))
for a,b,t in xr(0x14D7DCC):
    if idc.print_insn_mnem(a) == "mov" and idc.get_operand_type(a,0) == idc.o_mem:
        P("W", hex(a), b and hex(b), idc.GetDisasm(a))
        if b: dec(b)
# callers of E539E0 and the a5 values passed (look at push imm before call)
cnt = 0
for a,b,t in xr(0xE539E0):
    if not b: continue
    ea = a; vals = []
    for _ in range(8):
        ea = idc.prev_head(ea, 0)
        if idc.print_insn_mnem(ea) == "push":
            vals.append(idc.print_operand(ea, 0))
    cnt += 1
    if cnt < 40: P("CALL_E539E0", hex(a), hex(b), vals[:4])
