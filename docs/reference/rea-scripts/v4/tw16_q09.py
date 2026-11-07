
for v in (0x85E090, 0x85E600, 0x85EA90, 0x85EE80, 0xB2B9C0, 0xBEBC60, 0xF457B0, 0xF4A8A0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:30])
