for v in (0x77F4B0, 0x790720, 0x7905C0, 0x78D860, 0x78F590, 0x402800):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:8])
P("BDC7D4 writers", [(hex(a), b and hex(b), idc.GetDisasm(a)) for a,b,t in xr(0xBDC7D4) if idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem])
P("C2F7DC writers", [(hex(a), b and hex(b), idc.GetDisasm(a)) for a,b,t in xr(0xC2F7DC) if idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem])
for a in (0x4b5d00,):
    dec(a)
