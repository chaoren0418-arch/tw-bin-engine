import idc
for fv in q.get("disasm", []):
    f = ida_funcs.get_func(int(fv, 16))
    lines = []
    for ea in idautils.FuncItems(f.start_ea):
        lines.append("%08X  %s" % (ea, idc.generate_disasm_line(ea, 0)))
    (out / ("%08X.asm" % f.start_ea)).write_text("\n".join(lines), encoding="utf-8")
