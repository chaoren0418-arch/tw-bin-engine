"""Decompile listed VAs (comma separated in REA_ARG); creates a function in the scratch IDB copy if missing."""
import sys, os; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import ida_funcs

def main(rep):
    for va in [int(x, 16) for x in ARG.split(",")]:
        f = ida_funcs.get_func(va)
        if not f:
            ida_funcs.add_func(va); ida_auto.auto_wait()
            f = ida_funcs.get_func(va)
        if not f:
            rep[hex(va)] = "nofunc"; continue
        dump(va)
        rep[hex(va)] = [hex(c) for c in callers(f.start_ea)][:60]
run(main)
