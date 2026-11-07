for fs in (0x405A20, 0x82B820):
    for ea in idautils.FuncItems(fs):
        for i in range(3):
            if idc.get_operand_type(ea,i) == idc.o_imm and (idc.get_operand_value(ea,i)&0xffffffff) in (0x1D6, 0x190, 0x7D):
                P("----", hex(fs), hex(ea)); P(dis(idc.prev_head(idc.prev_head(idc.prev_head(ea))), 9))
dec(0x82B820)
P("82B820 callers", sorted({hex(b) for a,b,t in xr(0x82B820) if b}))
