for va in (0x841770, 0xF188A0, 0xF191A0, 0xF19C90, 0xF18400, 0xF1A000, 0xF19F90, 0x933850, 0x9340B0):
    t = dec(va)
    P(f"==== {va:08X} len={len(t) if t else 0}")
for g in (0x154524C, 0x1545250, 0x1544514, 0x1656720):
    P(f"==== xrefs {g:08X}")
    for frm, fs, t in xr(g)[:60]:
        P(f"{frm:08X} in {fs if fs is None else hex(fs)} t={t}  {idc.GetDisasm(frm)}")
