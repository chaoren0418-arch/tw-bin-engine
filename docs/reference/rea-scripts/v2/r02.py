for va in (0xF04180, 0xF044A0, 0xA231C0):
    P(f"==== {va:08X}"); P(dec(va))
P("==== xrefs F04180")
for frm, fs, t in xr(0xF04180):
    P(f"{frm:08X} in {fs if fs is None else hex(fs)}")
    P(dis(idc.prev_head(idc.prev_head(idc.prev_head(idc.prev_head(idc.prev_head(idc.prev_head(idc.prev_head(idc.prev_head(idc.prev_head(frm,0),0),0),0),0),0),0),0),0), 10))
for base in (0x1544388, 0x154440C, 0x1544490, 0x1544598):
    P(f"==== xrefs {base:08X}")
    for frm, fs, t in xr(base):
        P(f"{frm:08X} in {fs if fs is None else hex(fs)} t={t}")
