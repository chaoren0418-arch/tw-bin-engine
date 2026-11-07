for v in (0x581E90, 0x82B7D0):
    t = dec(v); P(hex(v), (t or "").count("\n"))
P("154524C refs", [(hex(a), b and hex(b), idc.GetDisasm(a)) for a,b,t in xr(0x154524C) if idc.get_operand_type(a,0)==idc.o_mem][:20])
P("1545250 refs", [(hex(a), b and hex(b), idc.GetDisasm(a)) for a,b,t in xr(0x1545250) if idc.get_operand_type(a,0)==idc.o_mem][:20])