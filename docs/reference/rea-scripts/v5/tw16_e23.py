import re, ida_segment
for segn in (".rdata",".data"):
    seg = ida_segment.get_segm_by_name(segn)
    buf = ida_bytes.get_bytes(seg.start_ea, seg.end_ea-seg.start_ea) or b""
    for m in re.finditer(rb"[\x20-\x7e]{4,}", buf):
        s = m.group().decode()
        if re.search(r"(?i)resolution|screenwidth|screenheight|screen_w|screen_h|800x600|1024x768|%dx%d|%d x %d|fullscreen|windowmode|viewport|widescreen|gamescreen", s):
            ea = seg.start_ea+m.start()
            P(hex(ea), repr(s)[:90], [(hex(a), b and hex(b)) for a,b,t in xr(ea)][:5])
P("users 154524C", sorted({hex(b) for a,b,t in xr(0x154524C) if b and idc.print_insn_mnem(a)=="mov" and idc.get_operand_type(a,0)==idc.o_mem}))