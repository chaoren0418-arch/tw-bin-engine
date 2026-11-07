for v in (0x7927A0, 0x783A60, 0x782480, 0x792260, 0x78DA00, 0x78D560, 0x4B6530, 0x4B5D00, 0x40EE00, 0x42DEE0, 0x402800, 0x5DDED0, 0x5DD7B0, 0x5D6AE0, 0x5D3EE0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:20])
for tgt in (0x7927A0, 0x4B6530, 0x78DA00):
    for a,b,t in xr(tgt):
        P("xref", hex(tgt), "from", hex(a), b and hex(b), t)
        if b and b not in (0x7923E0,):
            dec(b)
P("ctx 4ba250"); P(dis(0x4BAE60, 40))
# vtable slot 22 of Status800
P("vt22", hex(ida_bytes.get_dword(0x8f057c + 88)), "vt Status slot22", hex(ida_bytes.get_dword(0x8f03ec + 88)))
# strings referenced in 4BA250 near the call
import re
f = ida_funcs.get_func(0x4BA250)
for ea in idautils.FuncItems(0x4BA250):
    if 0x4BAD00 <= ea <= 0x4BAF10:
        for i in range(2):
            if idc.get_operand_type(ea,i) == idc.o_imm:
                v = idc.get_operand_value(ea,i)
                s = idc.get_strlit_contents(v)
                if s: P("s", hex(ea), s)
