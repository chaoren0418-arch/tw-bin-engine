import re, ida_segment
seg = ida_segment.get_segm_by_name(".rdata")
data = ida_bytes.get_bytes(seg.start_ea, seg.end_ea - seg.start_ea)
dseg = ida_segment.get_segm_by_name(".data")
ddata = ida_bytes.get_bytes(dseg.start_ea, dseg.end_ea - dseg.start_ea) or b""
pat = re.compile(rb"[\x20-\x7e]{4,}")
for base, buf in ((seg.start_ea, data), (dseg.start_ea, ddata)):
    for m in pat.finditer(buf):
        s = m.group().decode()
        if re.search(r"(?i)macro|minimap|mini_map|minmap|hotkey|shortcut|keymap|keyboard|fieldmap|field_map|worldmap|\.png|map\\\\|screenshot", s):
            ea = base + m.start()
            refs = [(hex(a), b and hex(b)) for a, b, t in xr(ea)][:6]
            P(hex(ea), repr(s)[:120], refs)