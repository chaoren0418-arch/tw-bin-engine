
for v in (0x52D5D0, 0x879E60, 0x83BE30, 0x52D380, 0x541600, 0xA0CA20, 0x83A300, 0x83A6A0, 0x8BA680):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:20])
f = ida_funcs.get_func(0x52D8A0)
ea = f.start_ea
while ea < f.end_ea:
    P(f"{ea:08X}  {idc.GetDisasm(ea)}")
    ea = idc.next_head(ea, f.end_ea)
