# Status800 vtable
def vt_of(name_bytes):
    for s in find_all(name_bytes, 3):
        td = s - 8
        for ea in find_all(td.to_bytes(4,"little"), 40):
            col = ea - 12
            if ida_bytes.get_dword(col) != 0: continue
            for v in find_all(col.to_bytes(4,"little"), 10):
                yield v + 4
for vt in vt_of(b".?AVStatus800@@\x00"):
    fns=[]
    for i in range(80):
        f = ida_bytes.get_dword(vt+4*i)
        if not ida_funcs.get_func(f) or ida_funcs.get_func(f).start_ea != f: break
        fns.append(hex(f))
    P("VT Status800", hex(vt), fns)
for vt in vt_of(b".?AVStatus@@\x00"):
    P("VT Status", hex(vt))
P("ECCFC0 vt write:")
t = dec(0xECCFC0); P(t[:1500])
# writers of +0x1EC (this[123])
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 60000: continue
    for ea in idautils.FuncItems(fs):
        if idc.print_insn_mnem(ea) == "mov" and idc.get_operand_type(ea,0) == idc.o_displ and (idc.get_operand_value(ea,0)&0xffffffff) == 0x1EC:
            if 0xEC0000 <= fs < 0xEE0000:
                P("W1EC", hex(fs), hex(ea), idc.GetDisasm(ea))