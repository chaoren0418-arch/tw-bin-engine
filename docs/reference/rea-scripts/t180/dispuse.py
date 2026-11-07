"""T180 (#482): list functions whose instructions use any displacement in ARG (comma hex list, e.g. '1AC,13C'),
restricted to .text; report per-function hit counts; decompile functions that use all of them."""
import sys, os; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, idc, ida_funcs, ida_ua

def main(rep):
    want = [int(x, 16) for x in ARG.split(",")]
    hits = {}
    for fea in idautils.Functions(0x401000, 0x1000000):
        f = ida_funcs.get_func(fea)
        got = set()
        for ea in idautils.FuncItems(fea):
            insn = ida_ua.insn_t()
            if not ida_ua.decode_insn(insn, ea): continue
            for op in insn.ops:
                if op.type in (idc.o_displ, idc.o_phrase) and op.addr in want:
                    got.add(op.addr)
        if got:
            hits[hex(fea)] = sorted(hex(x) for x in got)
    rep["hits"] = hits
    for k, v in hits.items():
        if len(v) == len(want):
            dump(int(k, 16))
run(main)
