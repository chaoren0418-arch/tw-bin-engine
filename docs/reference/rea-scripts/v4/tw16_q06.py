
for nm in ("??_7HashFileManager@@6B@", "??_7FileManager@@6B@", "??_7LocalFileFormatter@@6B@", "??_7LocalFileNonDashFormatter@@6B@", "??_7FileMan@@6B@"):
    vt = idc.get_name_ea_simple(nm)
    if vt == idc.BADADDR:
        P("NOVT", nm); continue
    ents = []
    for i in range(24):
        f = ida_bytes.get_dword(vt + 4*i)
        g = ida_funcs.get_func(f)
        if not g or g.start_ea != f: break
        ents.append(hex(f)); dec(f)
    P("VT", nm, hex(vt), ents)
for v in (0x83B7C0, 0x632DD0, 0x52D0B0, 0x7D0F70):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"))
