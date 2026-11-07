def vt_of(name_bytes):
    for s in find_all(name_bytes, 3):
        td = s - 8
        for ea in find_all(td.to_bytes(4,"little"), 40):
            col = ea - 12
            if ida_bytes.get_dword(col) != 0: continue
            for v in find_all(col.to_bytes(4,"little"), 10):
                yield v + 4
todo = set()
for nm in (b".?AUStatus800@@\x00", b".?AUStatus@@\x00", b".?AUStatusMiniMap@@\x00", b".?AUStatusMacroExtend@@\x00", b".?AUStatusPartyPet@@\x00", b".?AUStatusBase@@\x00", b".?AUChatFrameSub@@\x00"):
    for vt in vt_of(nm):
        fns=[]
        for i in range(60):
            f = ida_bytes.get_dword(vt+4*i)
            if not ida_funcs.get_func(f) or ida_funcs.get_func(f).start_ea != f: break
            fns.append(f)
        users = sorted({b for a,b,t in xr(vt) if b})
        P("VT", nm, hex(vt), [hex(x) for x in fns], "users", [hex(x) for x in users])
        for u in users:
            todo.add(u)
            for a,b,t in xr(u):
                if b: P("  caller of", hex(u), "->", hex(b), "at", hex(a))
        if nm in (b".?AUStatus800@@\x00", b".?AUStatusMiniMap@@\x00", b".?AUStatusMacroExtend@@\x00", b".?AUStatusPartyPet@@\x00"):
            for f in fns: todo.add(f)
for f in sorted(todo):
    t = dec(f); P("DEC", hex(f), (t or "").count("\n"))
dec(0x5fe870)
