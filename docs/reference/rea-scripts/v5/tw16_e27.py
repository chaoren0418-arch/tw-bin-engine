from collections import defaultdict
hits = defaultdict(list)
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 80000: continue
    for ea in idautils.FuncItems(fs):
        if idc.print_insn_mnem(ea)=="mov" and idc.get_operand_type(ea,1)==idc.o_imm and idc.get_operand_type(ea,0) in (idc.o_mem, idc.o_displ):
            v = idc.get_operand_value(ea,1)&0xffffffff
            if v in (0x320,0x258,0x1D6,0x1E0,0x280,0x190,0x12C):
                hits[fs].append((hex(ea), idc.GetDisasm(ea)))
for fs, l in sorted(hits.items()):
    vs = " | ".join(d for a,d in l)
    if ("320h" in vs and ("258h" in vs or "1D6h" in vs)):
        P(hex(fs), len(l), vs[:400])