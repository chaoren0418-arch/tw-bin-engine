"""For each VA in ARG: bytes (64), whether initialized, code xrefs (funcs) to it; decompile first N referencing funcs."""
import sys, os; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, ida_funcs, idc, ida_bytes

def main(rep):
    done = set()
    for va in [int(x, 16) for x in ARG.split(",")]:
        b = ida_bytes.get_bytes(va, 64) if ida_bytes.is_loaded(va) else None
        refs = []
        for x in idautils.XrefsTo(va):
            f = ida_funcs.get_func(x.frm)
            refs.append((hex(x.frm), hex(f.start_ea) if f else None, x.iscode))
        rep[hex(va)] = {"bytes": b.hex() if b else None, "refs": refs[:40]}
        for _, fs, _ in refs[:6]:
            if fs and fs not in done:
                done.add(fs); dump(int(fs, 16))
run(main)
