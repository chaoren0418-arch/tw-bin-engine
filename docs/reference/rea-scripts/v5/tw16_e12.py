for v in (0x11E29E4, 0x11E2804, 0x11E29B4, 0x11E29C4, 0x119D0D4):
    P("raw", hex(v), [hex(x) for x in find_all(v.to_bytes(4,"little"))], [(hex(a), b and hex(b)) for a,b,t in xr(v)])
for s in (b"uistatus86\x00", b"uistatus86", b"uistatus\x00", b"uiextra86", b"MainStatusConfigulation"):
    for h in find_all(s, 10):
        P("str", s, hex(h), [(hex(a), b and hex(b)) for a,b,t in xr(h)])
# writers of dword_153F440
for a,b,t in xr(0x153F440):
    if idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem:
        P("W153F440", hex(a), b and hex(b), idc.GetDisasm(a))