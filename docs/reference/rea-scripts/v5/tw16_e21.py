P(dis(0xB59AB0, 22))
P("13CF594 writers", [(hex(a), b and hex(b), idc.GetDisasm(a)) for a,b,t in xr(0x13CF594) if idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem])
P("13CF590 refs", [(hex(a), b and hex(b)) for a,b,t in xr(0x13CF590)][:20])
for v in (0x8DBB40, 0x8DC930, 0x8DC8F0, 0x8DC730):
    t = dec(v); P(hex(v), (t or "").count("\n"))