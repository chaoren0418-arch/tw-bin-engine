"""For each VA in ARG: list data xrefs and dump +-N dwords around each ref, resolving strings/functions."""
import sys, os; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, ida_funcs, idc, ida_bytes, ida_name

def desc(v):
    s = idc.get_strlit_contents(v, -1, idc.STRTYPE_C)
    if s and len(s) > 1:
        return "str:" + s.decode("cp950", "replace")
    f = ida_funcs.get_func(v)
    if f and f.start_ea == v: return "func:" + hex(v)
    return hex(v)

def main(rep):
    n = 24
    for va in [int(x, 16) for x in ARG.split(",")]:
        out = []
        for x in idautils.XrefsTo(va):
            if x.iscode: continue
            rows = []
            for i in range(-n, n):
                a = x.frm + 4 * i
                rows.append("%s %s" % (hex(a), desc(ida_bytes.get_dword(a))))
            out.append({"ref": hex(x.frm), "rows": rows})
        rep[hex(va)] = out
run(main)
