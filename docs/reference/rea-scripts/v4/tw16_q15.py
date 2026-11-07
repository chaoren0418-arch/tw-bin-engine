
for v in (0xA842D0, 0xA85FD0, 0xA86E10, 0xA983A0, 0xA9AD80):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:10])
    lines = (t or "").split("\n")
    for i, l in enumerate(lines):
        if "surfNumber" in l or "126" in l and "~" in l:
            P("  L", i, "\n".join(lines[max(0, i-3): i+25]))
for s in [b"~\x00", b"%d~%d"]:
    for h in find_all(s, 10):
        P("STR", s, hex(h), [(hex(a), b and hex(b)) for a,b,t in xr(h)][:10])
