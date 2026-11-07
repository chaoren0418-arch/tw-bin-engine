for v in (0x8AECE0, 0x9001F0, 0x9061E0, 0x8B9C20, 0xE40290):
    t = dec(v); P(hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:6])