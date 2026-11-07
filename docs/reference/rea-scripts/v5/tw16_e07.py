def vt_of(name_bytes):
    for s in find_all(name_bytes, 3):
        td = s - 8
        for ea in find_all(td.to_bytes(4,"little"), 40):
            col = ea - 12
            if ida_bytes.get_dword(col) != 0: continue
            for v in find_all(col.to_bytes(4,"little"), 10):
                yield v + 4
for nm in (b".?AUStatus800@@\x00", b".?AUStatus@@\x00", b".?AUStatusMiniMap@@\x00", b".?AUChatFrameSub@@\x00", b".?AUStatusSub@@\x00"):
    for vt in vt_of(nm):
        fns=[]
        for i in range(120):
            f = ida_bytes.get_dword(vt+4*i)
            if not ida_funcs.get_func(f) or ida_funcs.get_func(f).start_ea != f: break
            fns.append(hex(f))
        P("VT", nm, hex(vt), len(fns), fns)
s800 = set()
for s in find_all(b".?AUStatus800@@\x00", 1): pass
# all writes of displacement 0x1EC whose source is loaded from same base +0x1F4/+0x1F8/+0x1FC (slot candidates)
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 60000: continue
    items = list(idautils.FuncItems(fs))
    has_w = any(idc.print_insn_mnem(ea)=="mov" and idc.get_operand_type(ea,0)==idc.o_displ and (idc.get_operand_value(ea,0)&0xffffffff)==0x1EC for ea in items)
    if not has_w: continue
    rd = {idc.get_operand_value(ea,1)&0xffffffff for ea in items if idc.get_operand_type(ea,1)==idc.o_displ}
    if {0x1F4,0x1F8}&rd:
        P("CAND", hex(fs), sorted(hex(x) for x in rd & {0x1F4,0x1F8,0x1FC,0x1E8}))