
for v in (0xA97680, 0xE17350, 0x7D0F70, 0x7D09F0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:12])
# who references the table of UI xml names at 0x119c868..
for ea in (0x119C868, 0x119C884, 0x119C928):
    for h in find_all(ea.to_bytes(4, "little"), 5):
        P("TBLREF", hex(ea), "at", hex(h), [(hex(a), b and hex(b)) for a,b,t in xr(h)][:6])
for s in [b"encoding", b"UTF-8", b"EUC-KR", b"BIG5", b"big5"]:
    for h in find_all(s, 10):
        P("STR", s, hex(h), repr(idc.get_strlit_contents(h) or b"")[:50], [(hex(a), b and hex(b)) for a,b,t in xr(h)][:5])
