for v in (0xED8940, 0xED8950, 0xEDAF00, 0xECED60, 0xED4530, 0xECF7D0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:12])
for v in (0x13CE7FC, 0x136DC5C, 0x1542324, 0x1542344, 0x154524C, 0x1545250, 0x13CE5EA):
    refs = xr(v)
    P("REF", hex(v), len(refs), sorted({hex(b) for a,b,t in refs if b})[:40])
    for a,b,t in refs:
        if idc.print_insn_mnem(a) == "mov" and idc.get_operand_type(a,0) == idc.o_mem:
            P("  W", hex(a), b and hex(b), idc.GetDisasm(a))
# Status800 / StatusPartyPet vtables
def vt_of(name_bytes):
    for s in find_all(name_bytes, 3):
        td = s - 8
        for ea in find_all(td.to_bytes(4,"little"), 40):
            col = ea - 12
            if ida_bytes.get_dword(col) != 0: continue
            for v in find_all(col.to_bytes(4,"little"), 10):
                yield v + 4
for nm in (b".?AUStatusPartyPet@@\x00", b".?AUStatusBase@@\x00"):
    for vt in vt_of(nm):
        fns=[]
        for i in range(40):
            f = ida_bytes.get_dword(vt+4*i)
            if not ida_funcs.get_func(f) or ida_funcs.get_func(f).start_ea != f: break
            fns.append(hex(f))
        P("VT", nm, hex(vt), fns, "users", sorted({hex(b) for a,b,t in xr(vt) if b}))
# functions touching disp 0x1EC and 0x1F0 (cur sub / partypet) anywhere
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 80000: continue
    rd = set()
    for ea in idautils.FuncItems(fs):
        for i in range(2):
            if idc.get_operand_type(ea,i)==idc.o_displ:
                rd.add(idc.get_operand_value(ea,i)&0xffffffff)
    if {0x1EC, 0x1F0, 0x1F4} <= rd or {0x1EC, 0x1F4, 0x1F8} <= rd:
        P("CAND3", hex(fs), fn.size())
