import idc
for fv in q.get("disasm", []):
    f = ida_funcs.get_func(int(fv, 16))
    lines = []
    for ea in idautils.FuncItems(f.start_ea):
        lines.append("%08X  %s" % (ea, idc.generate_disasm_line(ea, 0)))
    (out / ("%08X.asm" % f.start_ea)).write_text("\n".join(lines), encoding="utf-8")
names = {}
for g in q.get("globals", []):
    ea = int(g, 16)
    xs = []
    for x in idautils.XrefsTo(ea):
        fs = fstart(x.frm)
        xs.append("%s@%s:%s" % (hex(fs or 0), hex(x.frm), idc.generate_disasm_line(x.frm, 0)))
    names[g] = {"val": hex(ida_bytes.get_dword(ea)), "xrefs": xs[:60]}
rep["globals"] = names
