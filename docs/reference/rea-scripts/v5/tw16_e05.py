for v in (0xECD6E0, 0xA695E0, 0xA6B110, 0xA703C0, 0xECD9D0):
    t = dec(v); P(hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:20])