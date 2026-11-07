
for v in (0xE538D0, 0x881910):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", len({b for a,b,tt in xr(v) if b}), sorted({hex(b) for a,b,tt in xr(v) if b})[:200])
