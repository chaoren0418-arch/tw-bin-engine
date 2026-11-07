for frm, fs, t in xr(0xA9AD80):
    P("xref A9AD80", hex(frm), fs and hex(fs), t)
    # dump vtable neighbourhood
    base = frm - 0x40
    P(" ".join(hex(ida_bytes.get_dword(base + 4*i)) for i in range(32)))
want = {0x1C4,0x1C8,0x1CC,0x1D0,0x1D4,0x1D8}
from collections import defaultdict
hits = defaultdict(set)
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 40000: continue
    for ea in idautils.FuncItems(fs):
        for i in range(2):
            if idc.get_operand_type(ea, i) == idc.o_displ:
                v = idc.get_operand_value(ea, i) & 0xffffffff
                if v in want: hits[fs].add(v)
for fs, s in sorted(hits.items()):
    if len(s) >= 2 or (0x1CC in s) or (0x1D4 in s):
        P("disp", hex(fs), sorted(hex(x) for x in s))