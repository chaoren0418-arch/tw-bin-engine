for v in (0x77F510, 0x5DD7B0, 0x4EA410):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:8])
for a,b,t in xr(0x77F510):
    P("xref 77F510", hex(a), b and hex(b), t); P(dis(a-0x20, 12))
