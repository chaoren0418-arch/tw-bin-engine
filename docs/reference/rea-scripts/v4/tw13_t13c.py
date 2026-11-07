
for ea in (0x5FEF9D, 0x5FF005, 0x5FFB54):
    P("=== at", hex(ea)); P(dis(ea - 6, 8))
    # follow the "equal" path: after test eax,eax / jnz X, the fallthrough is the equal branch
    e = ea
    for _ in range(8):
        e = idc.next_head(e, ida_ida.inf_get_max_ea())
        if idc.print_insn_mnem(e) in ("jnz", "jz"):
            break
    P("branch", idc.GetDisasm(e))
    tgt = idc.get_operand_value(e, 0)
    P("-- fallthrough"); P(dis(idc.next_head(e, ida_ida.inf_get_max_ea()), 40))
    P("-- target"); P(dis(tgt, 10))
