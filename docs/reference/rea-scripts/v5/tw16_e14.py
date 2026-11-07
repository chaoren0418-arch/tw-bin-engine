import re, ida_segment
names = []
for segn in (".data", ".rdata"):
    seg = ida_segment.get_segm_by_name(segn)
    buf = ida_bytes.get_bytes(seg.start_ea, seg.end_ea-seg.start_ea) or b""
    for m in re.finditer(rb"\.\?A[UV][A-Za-z0-9_@?$]+@@", buf):
        s = m.group().decode()
        if "LineageWidget" in s or re.search(r"(?i)slot|status|hud|minimap|macro|mainui|gameui|bottom", s):
            names.append((hex(seg.start_ea+m.start()), s))
for a,s in names: P(a, s)
for v in (0x7DD6B0, 0x7DEC80, 0x8C7C80, 0xAC6140, 0xE22F60, 0xECC770):
    t = dec(v) or ""
    offs = sorted(set(re.findall(r"dword_153F440 \+ (\d+)", t)))
    P("user", hex(v), offs, "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:8])