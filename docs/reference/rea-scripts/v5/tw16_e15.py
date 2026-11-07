P("B56A40 callers", [(hex(a), b and hex(b)) for a,b,t in xr(0xB56A40)])
for v in (0x707020, 0x6FBA30, 0x9F82E0, 0xF6B4A0, 0x7021B0):
    t = dec(v); P(hex(v), (t or "").count("\n"))
for s in (b"MiniMapUI\x00", b"WidgetSlot\x00", b"MainButtonUI\x00", b"WidgetSubSlot\x00", b"SlotWidgetUI\x00", b".xml\x00", b"%s.xml\x00", b"UIList", b"uilist"):
    for h in find_all(s, 12):
        P("str", s, hex(h), [(hex(a), b and hex(b)) for a,b,t in xr(h)][:8])