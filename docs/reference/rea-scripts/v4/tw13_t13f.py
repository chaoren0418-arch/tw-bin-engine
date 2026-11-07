
def vts_in(fn):
    r = []
    for ea in idautils.FuncItems(fn):
        if idc.print_insn_mnem(ea) == "mov" and idc.get_operand_type(ea, 1) == idc.o_imm:
            v = idc.get_operand_value(ea, 1)
            if ida_bytes.get_dword(v - 4) and ida_funcs.get_func(ida_bytes.get_dword(v)):
                r.append(v)
    return r
dec(0x50C290)
for fn in (0x50C880, 0x50C290):
    for vt in vts_in(fn):
        ents = []
        for i in range(40):
            f = ida_bytes.get_dword(vt + 4*i); g = ida_funcs.get_func(f)
            if not g or g.start_ea != f: break
            ents.append(f)
        P("VT", hex(fn), hex(vt), ida_name.get_name(vt), [hex(e) for e in ents])
        for e in ents:
            t = dec(e) or ""
            P("   m", hex(e), t.count("\n"), t.split("\n")[0][:110])
