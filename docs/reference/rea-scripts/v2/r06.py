ea = ida_name.get_name_ea(idc.BADADDR, "aAvFuncImplUCal_48")
P(f"aAvFuncImplUCal_48 = {ea:08X}  f-table base(+64+4*48) = {ea+64+192:08X}")
P(dis(0x841770, 1))
for va in (0xF19CD0, 0xF17E30, 0xF19D40, 0xF19E40, 0xF187C0, 0xF19E70, 0xA22830, 0xF18240):
    t = dec(va); P(f"==== {va:08X}"); P(t)
