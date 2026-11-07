P(dis(0xF101F0, 14))
f = ida_funcs.get_func(0x94FB00)
ea = f.start_ea
while ea < f.end_ea:
    d = idc.GetDisasm(ea)
    if "3A98h" in d or "3EEh" in d or "3F9h" in d or "725h" in d or "22Ah" in d or "229h" in d or "sub_9340B0" in d or "sub_933850" in d:
        P(f"{ea:08X}  {d}")
    ea = idc.next_head(ea, f.end_ea)
for g in (0x1544388, 0x1369D70, 0x153C998):
    P(f"{g:08X} seg={idc.get_segm_name(g)}")
