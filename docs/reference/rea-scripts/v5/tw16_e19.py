for v in (0x9FA0D0, 0x500640, 0x8BA150, 0x8DCC40, 0x7D3400, 0x7D3640, 0x9FAAD0, 0x8BD990, 0xAC22D0):
    t = dec(v); P(hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:10])
for a,b,t in xr(0x13CE3E0):
    d = idc.GetDisasm(a)
    if "4000" in d or "or" in idc.print_insn_mnem(a) or "and" in idc.print_insn_mnem(a) or idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem:
        P("13CE3E0", hex(a), b and hex(b), d)
P("13AA8D8 writers", [(hex(a), b and hex(b)) for a,b,t in xr(0x13AA8D8) if idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem])
P("13CF590 writers", [(hex(a), b and hex(b)) for a,b,t in xr(0x13CF590) if idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem])