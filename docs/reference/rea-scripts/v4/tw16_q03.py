
P(dis(0xB3D49D, 60))
for v in (0xB3CD40, 0xB3D640, 0xB3D770, 0x5265A0, 0x66E050, 0x66D980, 0x97CF30, 0xE51F60):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:30])
for fn in (0xE540D0, 0xE55DD0, 0xE55C50):
    for h in find_all(fn.to_bytes(4, "little"), 10):
        P("VTREF", hex(fn), "at", hex(h))
        base = h
        while ida_funcs.get_func(ida_bytes.get_dword(base - 4)) and ida_funcs.get_func(ida_bytes.get_dword(base-4)).start_ea == ida_bytes.get_dword(base-4):
            base -= 4
        ents = []
        for i in range(30):
            f = ida_bytes.get_dword(base + 4*i)
            g = ida_funcs.get_func(f)
            if not g or g.start_ea != f: break
            ents.append(hex(f))
        P("  VT", hex(base), ents, "users", sorted({hex(b) for a,b,t in xr(base) if b}))
        for e in ents:
            dec(int(e, 16))
for s in [b".idx", b".pak", b"%02d", b"%s%02d", b"%s%d", b"Sprite0", b"sprite%", b"Text.pak", b"Tile", b"Icon"]:
    hits = find_all(s, 20)
    P("STR", s, len(hits))
    for h in hits:
        P("  ", hex(h), repr(idc.get_strlit_contents(h) or b"")[:60], [(hex(a), b and hex(b)) for a,b,t in xr(h)][:10])
