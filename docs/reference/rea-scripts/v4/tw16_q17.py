
for g in (0x13CD54C, 0x13CD2F0, 0x13CD2F4, 0x13CD5A0, 0x13CD559):
    P("REF", hex(g), sorted({hex(b) for a,b,t in xr(g) if b})[:40])
for v in (0x83FB80, 0x83DF40, 0x83E490, 0x843C50, 0x8449E0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", len({b for a,b,tt in xr(v) if b}), sorted({hex(b) for a,b,tt in xr(v) if b})[:15])
# heights passed to sub_856B80
from collections import Counter
hc = Counter()
for a,b,t in xr(0x856B80):
    ea = a; ps = []
    for _ in range(6):
        ea = idc.prev_head(ea, 0)
        if idc.print_insn_mnem(ea) == "push": ps.append(idc.print_operand(ea, 0))
    P("CALL856", hex(a), b and hex(b), ps[:3])
