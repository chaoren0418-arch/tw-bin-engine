for a in (0x4116A0,):
    for ea in idautils.FuncItems(a):
        for i in range(2):
            if idc.get_operand_type(ea,i) in (idc.o_imm, idc.o_mem):
                v = idc.get_operand_value(ea,i)
                s = idc.get_strlit_contents(v)
                if s and (b"uitype" in s.lower() or b"option" in s.lower()): P("s4116a0", hex(ea), hex(v), s)
for v in (0x9A9180,):
    P("9A9180", idc.get_strlit_contents(v), ida_bytes.get_bytes(v, 24))
P("4D5321 ctx"); P(dis(0x4D52F0, 20))
for v in (0x5BCDD0, 0x4488B0, 0x782480):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"))
res = {}
for fs in idautils.Functions():
    fn = ida_funcs.get_func(fs)
    if fn.size() > 80000: continue
    vals=set()
    for ea in idautils.FuncItems(fs):
        for i in range(3):
            if idc.get_operand_type(ea,i) == idc.o_imm:
                v = idc.get_operand_value(ea,i)&0xffffffff
                if v in (0x190,0xEB,0x12C,0x1D6,0x1D5,0x320,0x258,0x82,0x7D): vals.add(v)
    if (0x190 in vals and (0xEB in vals or 0x12C in vals)) or 0x1D6 in vals:
        res[fs]=vals
for fs, v in sorted(res.items()):
    P("CAND", hex(fs), ida_funcs.get_func(fs).size(), sorted(hex(x) for x in v), "callers", len(list(idautils.XrefsTo(fs))))
    if ida_funcs.get_func(fs).size() < 6000: dec(fs)
