res = {}
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 80000: continue
    vals=set()
    for ea in idautils.FuncItems(fs):
        for i in range(3):
            ot = idc.get_operand_type(ea,i)
            if ot == idc.o_imm:
                v = idc.get_operand_value(ea,i)&0xffffffff
                if v in (0x320,0x258,0x1D6,0x1D5,0x1E0,0x280,0x7D): vals.add(v)
    if (0x320 in vals and 0x258 in vals) or 0x1D6 in vals or 0x1D5 in vals:
        res[fs]=vals
for fs, v in sorted(res.items()):
    P(hex(fs), ida_funcs.get_func(fs).size(), sorted(hex(x) for x in v), "callers", len(list(idautils.XrefsTo(fs))))