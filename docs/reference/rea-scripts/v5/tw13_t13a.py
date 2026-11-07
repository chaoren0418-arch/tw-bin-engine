P("funcs", len(list(idautils.Functions())))
for s in (b"MINI_MAP_BASE\x00", b"MACRO_ICON\x00", b"PARTY_PET_RT_WINDOW\x00", b"EX_MACRO_WINDOW\x00", b".?AUStatus800@@\x00", b".?AUStatusPartyPet@@\x00", b".?AUStatusMiniMap@@\x00", b".?AUStatusMacroExtend@@\x00"):
    for h in find_all(s, 3):
        P("str", s, hex(h), [(hex(a), b and hex(b)) for a,b,t in xr(h)][:6])
