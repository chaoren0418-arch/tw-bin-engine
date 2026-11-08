
dec(0x77F6B0)
# field offsets in CUIStatusXML parser for each element name (TW13 sub_5FE870): string xref -> next "mov [reg+X], eax"
import re
names = [b"LEVEL_BTN", b"BALOG_BTN", b"ADENA_BTN", b"CARRY_WARN_BTN", b"SHOUT_BTN", b"FOOD_WARN_BTN", b"MAP_BTN", b"MENU_BTN"]
for s in idautils.Strings():
    pass
for nm in names:
    for h in find_all(nm + b"\x00", 3):
        for a, b, t in xr(h):
            if b != 0x5FE870: continue
            e = a
            for _ in range(40):
                e = idc.next_head(e, ida_ida.inf_get_max_ea())
                d = idc.GetDisasm(e)
                m = re.match(r"mov\s+\[e[a-z]x\+([0-9A-F]+)h\], e[a-z]x", d)
                if m:
                    P("FIELD", nm, "0x" + m.group(1), int(m.group(1), 16)); break
# all element names compared in 5FE870 with their field
for a in idautils.FuncItems(0x5FE870):
    if idc.print_insn_mnem(a) == "push" and idc.get_operand_type(a, 0) == idc.o_imm:
        v = idc.get_operand_value(a, 0)
        s = idc.get_strlit_contents(v)
        if s and s.isupper() is False and not s.replace(b"_", b"").isalnum(): continue
        if s and s.upper() == s and len(s) > 3:
            e = a
            for _ in range(40):
                e = idc.next_head(e, ida_ida.inf_get_max_ea())
                d = idc.GetDisasm(e)
                if "push    offset" in d: break
                m = re.match(r"mov\s+\[e[a-z]x\+([0-9A-F]+)h\], e[a-z]x", d)
                if m:
                    P("EL", s.decode(), int(m.group(1), 16)); break
