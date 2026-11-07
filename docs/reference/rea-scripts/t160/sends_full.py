"""All calls to sender 0x9FBC00 with up to 6 pushed operands (fmt, opcode, args...). Output sends.json."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, ida_funcs, idc

SEND = 0x9FBC00

def main(rep):
    res = []
    for x in idautils.XrefsTo(SEND):
        if not x.iscode: continue
        f = ida_funcs.get_func(x.frm)
        lo = f.start_ea if f else x.frm - 0x80
        ea = x.frm; pushes = []
        for _ in range(60):
            ea = idc.prev_head(ea, lo)
            if ea == idc.BADADDR or ea < lo: break
            m = idc.print_insn_mnem(ea)
            if m == "call" and idc.get_operand_value(ea, 0) != SEND:
                pass
            if m == "push":
                pushes.append(idc.print_operand(ea, 0))
                if len(pushes) >= 6: break
        fmt = None
        if pushes:
            s = idc.get_strlit_contents(idc.get_name_ea_simple(pushes[0].replace("offset ", "")))
            fmt = s.decode("latin1") if s else pushes[0]
        res.append({"site": hex(x.frm), "func": hex(f.start_ea) if f else None, "fmt": fmt, "pushes": pushes})
    (OUT / "sends.json").write_text(json.dumps(res, indent=0), encoding="utf-8")
    rep["n"] = len(res)
run(main)
