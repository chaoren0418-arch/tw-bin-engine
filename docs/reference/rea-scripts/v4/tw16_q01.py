
# V4 q01: strings for img loader, surf list, sprite pak names, fonts, xml loader
for s in [b".img\x00", b"%d.img", b"%s.img", b"SurfList", b"Sprite%02d", b"Sprite%d", b"Sprite.idx", b"Sprite.pak", b"sprite", b"Sprite",
          b"%s-%c.xml", b"surfNumber", b"encoding", b"XML Encryption Check",
          "細明體".encode("cp950"), "新細明體".encode("cp950"), b"MingLiU", b"PMingLiU", b"Gulim", b"Tahoma", b"Arial", b"FontName", b"fontsize", b"Font"]:
    hits = find_all(s, 30)
    P("STR", s, len(hits))
    for h in hits:
        refs = xr(h)
        P("  ", hex(h), repr(idc.get_strlit_contents(h) or b"")[:60], [(hex(a), b and hex(b)) for a,b,t in refs][:12])
for imp in ["CreateFontA", "CreateFontW", "CreateFontIndirectA", "CreateFontIndirectW", "MultiByteToWideChar", "GetTextExtentPoint32A", "TextOutA", "ExtTextOutA", "ExtTextOutW", "TextOutW"]:
    ea = idc.get_name_ea_simple(imp)
    if ea == idc.BADADDR:
        ea = idc.get_name_ea_simple("__imp_" + imp)
    P("IMP", imp, hex(ea) if ea != idc.BADADDR else None, sorted({hex(b) for a,b,t in xr(ea) if b})[:40] if ea != idc.BADADDR else None)
t = dec(0xB3D390); P("DEC B3D390 callers", sorted({hex(b) for a,b,t in xr(0xB3D390) if b})[:40])
