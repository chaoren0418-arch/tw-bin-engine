fs = sorted({b for a,b,t in xr(0xC2F850) if b and 0x77F000 <= b <= 0x793000})
P("n", len(fs))
for f in fs:
    t = dec(f) or ""
    if "+ 460)" in t or "+ 456)" in t:
        P("USES460", hex(f), t.count("\n"))
