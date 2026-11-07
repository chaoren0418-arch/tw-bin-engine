for v in (0x81AC20, 0x81AF20, 0x81A720, 0xA67070, 0x9DCB20, 0xA6B110, 0xA6A090, 0x707210, 0x8E05C0, 0xF6B250):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:12])
res = {}
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 80000: continue
    vals=set()
    for ea in idautils.FuncItems(fs):
        for i in range(3):
            if idc.get_operand_type(ea,i) == idc.o_imm:
                v = idc.get_operand_value(ea,i)&0xffffffff
                if v in (0x190,0xEB,0x12C,0x1D6,0x1D5,0x1D3,0x1D7,0x82,0x7D,0xEA,0xEC): vals.add(v)
    if 0x190 in vals and (0xEB in vals or 0xEA in vals or 0xEC in vals):
        res[fs]=vals
for fs, v in sorted(res.items()):
    P("CENTER", hex(fs), ida_funcs.get_func(fs).size(), sorted(hex(x) for x in v), "callers", len(list(idautils.XrefsTo(fs))))
