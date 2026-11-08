
for v in (0x52D010, 0x52CF50, 0x52D450, 0x52D3A0, 0x52D470, 0x52D3C0, 0x839F60, 0x52D4B0, 0x52D4E0, 0x52DD80, 0x632DF0, 0x632E50, 0xA97450, 0xA97680):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:30])
P(dis(0x66DAB0 + 0, 5))
# vtables assigned in 52D010 / 52CF50 / 839F60
for fn in (0x52D010, 0x52CF50, 0x839F60, 0x632DF0, 0x632E50):
    for ea in idautils.FuncItems(fn):
        if idc.print_insn_mnem(ea) == "mov" and idc.get_operand_type(ea, 1) == idc.o_imm:
            vt = idc.get_operand_value(ea, 1)
            nm = ida_name.get_name(vt)
            if "vftable" in (nm or "") or "off_" in (nm or ""):
                ents = []
                for i in range(20):
                    f = ida_bytes.get_dword(vt + 4*i)
                    g = ida_funcs.get_func(f)
                    if not g or g.start_ea != f: break
                    ents.append(hex(f)); dec(f)
                P("VT", hex(fn), hex(vt), nm, ents)
