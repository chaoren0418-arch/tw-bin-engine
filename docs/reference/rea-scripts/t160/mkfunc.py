"""ARG='start:end,start:end' -> force-create functions in the scratch IDB copy, then decompile them."""
import sys, os; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import ida_funcs, idc, ida_ua

def main(rep):
    for pair in ARG.split(","):
        a, b = [int(x, 16) for x in pair.split(":")]
        ea = a
        while ea < b:
            ida_ua.create_insn(ea); ea += idc.get_item_size(ea) or 1
        old = ida_funcs.get_func(a)
        ok = ida_funcs.add_func(a, b)
        ida_auto.auto_wait()
        f = ida_funcs.get_func(a)
        rep[hex(a)] = {"ok": bool(ok), "func": hex(f.start_ea) if f else None, "end": hex(f.end_ea) if f else None}
        if f: dump(a)
run(main)
