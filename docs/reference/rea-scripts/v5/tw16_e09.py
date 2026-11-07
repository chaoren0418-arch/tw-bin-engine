for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 80000: continue
    rd = set()
    for ea in idautils.FuncItems(fs):
        for i in range(2):
            if idc.get_operand_type(ea,i)==idc.o_displ:
                rd.add(idc.get_operand_value(ea,i)&0xffffffff)
    if 0x1EC in rd and (0x1F4 in rd or 0x1F8 in rd or 0x1FC in rd):
        P("CAND", hex(fs), fn.size(), sorted(hex(x) for x in rd & {0x1E8,0x1EC,0x1F0,0x1F4,0x1F8,0x1FC}))
P("callers ED33F0", [(hex(a), b and hex(b)) for a,b,t in xr(0xED33F0)])