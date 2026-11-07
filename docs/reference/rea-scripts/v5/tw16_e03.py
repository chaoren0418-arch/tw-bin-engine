for v in (0xA842D0, 0xAD8EF0, 0xB56A40, 0xE557D0, 0xE53FF0, 0xA730A0):
    t = dec(v); P(hex(v), (t or "").count("\n"))
def vtables(rtti_str_ea):
    td = rtti_str_ea - 8
    res = []
    for ea in find_all(td.to_bytes(4, "little"), 40):
        col = ea - 12
        if ida_bytes.get_dword(col) != 0: continue
        for v in find_all(col.to_bytes(4, "little"), 10):
            res.append(v + 4)
    return res
for name, ea in (("StatusMiniMap",0x138b8a0),("StatusMacroExtend",0x138b8bc),("MacroWindow",0x1369044),("MiniMapUI",0x1363f50),("LW::MiniMapUI",0x13912ec),("MiniMapFieldViewer",0x13715fc),("MiniMapDungeonViewer",0x1370b0c),("FieldMapWindow",0x137af48)):
    for vt in vtables(ea):
        fns = [ida_bytes.get_dword(vt + 4*i) for i in range(40)]
        out_ = []
        for f in fns:
            if not ida_funcs.get_func(f) or ida_funcs.get_func(f).start_ea != f: break
            out_.append(hex(f))
        ctors = sorted({hex(b) for a, b, t in xr(vt) if b})
        P("VT", name, hex(vt), "ctor/users", ctors, "slots", out_)