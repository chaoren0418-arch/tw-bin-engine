import ida_segment, struct
seg = ida_segment.get_segm_by_name(".text")
buf = ida_bytes.get_bytes(seg.start_ea, seg.end_ea-seg.start_ea)
tg = {0xECD6E0, 0xECEC20, 0xECEA70, 0xA959D0, 0xAA0610}
for i in range(len(buf)-5):
    if buf[i] in (0xE8, 0xE9):
        d = struct.unpack_from("<i", buf, i+1)[0]
        t = (seg.start_ea + i + 5 + d) & 0xffffffff
        if t in tg: P("rel", hex(buf[i]), hex(t), "at", hex(seg.start_ea+i), fstart(seg.start_ea+i) and hex(fstart(seg.start_ea+i)))
for v in (0xA959D0, 0xAA0610):
    P(hex(v), [(hex(a), b and hex(b)) for a,b,t in xr(v)])
P("tbl", [hex(ida_bytes.get_dword(0x137B3A0+4*i)) for i in range(12)])
for v in (0x137B3B0, 0x137B3B4, 0x137B3A0, 0x137B3A8):
    P("tblref", hex(v), [(hex(a), b and hex(b)) for a,b,t in xr(v)])
t = dec(0xAA0610); P(t[:4000])