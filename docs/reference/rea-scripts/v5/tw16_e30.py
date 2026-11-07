for v in (0xE557D0, 0xE53FF0):
    P(hex(v), "callers", sorted({hex(b) for a,b,t in xr(v) if b}))
for v in (0xA6A0D0, 0xA6BD00):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:8])
