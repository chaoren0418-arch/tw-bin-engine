"""Functions whose cmp/case immediates cover >= K of the set ARG='K;v1,v2,...' (decimal). Lists top 40 by coverage."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, idc, ida_funcs

def main(rep):
    k, vals = ARG.split(";")
    k = int(k); want = set(int(v) for v in vals.split(","))
    res = []
    for fea in idautils.Functions():
        f = ida_funcs.get_func(fea)
        if f.size() > 0x4000: continue
        seen = set()
        for h in idautils.Heads(f.start_ea, f.end_ea):
            m = idc.print_insn_mnem(h)
            if m in ("cmp", "sub", "add", "push", "mov") and idc.get_operand_type(h, 1 if m != "push" else 0) == idc.o_imm:
                v = idc.get_operand_value(h, 1 if m != "push" else 0)
                if v in want: seen.add(v)
        if len(seen) >= k:
            res.append((len(seen), hex(fea), sorted(seen)))
    res.sort(reverse=True)
    rep["res"] = res[:40]
run(main)
