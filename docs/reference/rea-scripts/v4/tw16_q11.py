
for v in (0xE524F0, 0xE52450, 0xE57E20, 0xE571D0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", len({b for a,b,tt in xr(v) if b}), sorted({hex(b) for a,b,tt in xr(v) if b})[:80])
