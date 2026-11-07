"""All calls to receive parser 0x9340B0 with their format string; output recv.json [{func, site, fmt}]."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, idc, ida_funcs

PARSE = 0x9340B0

def main(rep):
    res = []
    for x in idautils.XrefsTo(PARSE):
        if not x.iscode: continue
        f = ida_funcs.get_func(x.frm)
        ea = x.frm; fmt = None; n = 0
        while n < 12:
            ea = idc.prev_head(ea, f.start_ea if f else 0)
            if ea == idc.BADADDR: break
            if idc.print_insn_mnem(ea) == "push":
                n += 1
                s = idc.get_strlit_contents(idc.get_operand_value(ea, 0))
                if s and all(c in b"cdhsbSDHB" for c in s):
                    fmt = s.decode(); break
        res.append({"func": hex(f.start_ea) if f else None, "site": hex(x.frm), "fmt": fmt,
                    "callers": [hex(c) for c in callers(f.start_ea)][:6] if f else []})
    (OUT / "recv.json").write_text(json.dumps(res, indent=0), encoding="utf-8")
    rep["n"] = len(res)
run(main)
