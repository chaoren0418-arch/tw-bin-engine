for v in (0xB56510, 0xE59800):
    t = dec(v); P(hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:10])
P(repr(ida_bytes.get_bytes(0x11dfcd0, 0x90)))