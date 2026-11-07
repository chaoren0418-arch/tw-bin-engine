
for v in (0x83DC90, 0x83E100, 0x8439D0, 0x83EAA0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", len({b for a,b,tt in xr(v) if b}), sorted({hex(b) for a,b,tt in xr(v) if b})[:12])
