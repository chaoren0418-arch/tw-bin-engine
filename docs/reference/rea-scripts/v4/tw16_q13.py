
import re
callers = sorted({b for a,b,t in xr(0xE538D0) if b})
calls_of = {}
for c in callers:
    fn = ida_funcs.get_func(c)
    callees = set()
    for ea in idautils.FuncItems(c):
        if idc.print_insn_mnem(ea) == "call" and idc.get_operand_type(ea, 0) in (idc.o_near, idc.o_far):
            callees.add(idc.get_operand_value(ea, 0))
    for x in callees:
        calls_of[x] = calls_of.get(x, 0) + 1
top = sorted(calls_of.items(), key=lambda kv: -kv[1])[:40]
P("TOPCALLEES", [(hex(a), n) for a, n in top])
for a, n in top[:25]:
    t = dec(a) or ""
    P("C", hex(a), n, t.count("\n"), t.split("\n")[0][:120])
