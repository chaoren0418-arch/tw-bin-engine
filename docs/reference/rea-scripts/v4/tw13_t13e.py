
for v in (0x50C880, 0x50E400):
    t = dec(v) or ""
    P("DEC", hex(v), t.count("\n"), "callers", len({b for a,b,tt in xr(v) if b}))
    # vtable assigned
    for ea in idautils.FuncItems(v):
        if idc.print_insn_mnem(ea) == "mov" and idc.get_operand_type(ea, 1) == idc.o_imm:
            vt = idc.get_operand_value(ea, 1); nm = ida_name.get_name(vt) or ""
            if "vftable" in nm:
                ents = []
                for i in range(40):
                    f = ida_bytes.get_dword(vt + 4*i); g = ida_funcs.get_func(f)
                    if not g or g.start_ea != f: break
                    ents.append(hex(f))
                P("VT", hex(v), hex(vt), nm, ents)
                for e in ents[:40]:
                    tt = dec(int(e, 16)) or ""
                    if "+ 1" in tt or "+ 2" in tt or "sub_" in tt:
                        P("   m", e, tt.count("\n"))
