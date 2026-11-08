
import re
for ctor in (0x50C880, 0x50C290):
    for fs in sorted({b for a,b,t in xr(ctor) if b}):
        t = dec(fs, save=False) or ""
        L = t.split("\n")
        for i, l in enumerate(L):
            if ("sub_%X(" % ctor) in l:
                P("CALL", hex(ctor), hex(fs), " ".join(x.strip() for x in L[i:i+4])[:260])
