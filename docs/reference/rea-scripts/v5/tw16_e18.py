for v in (0x87C890, 0x8C1290, 0x8C21B0):
    t = dec(v); P(hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:10])