"""Dump palette dwords at 0x1369D58.. (ARG='base,count'), plus every instruction writing into that range."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, idc, ida_bytes, ida_funcs

def main(rep):
    base, n = ARG.split(",")
    base = int(base, 16); n = int(n)
    rep["loaded"] = bool(ida_bytes.is_loaded(base))
    rep["values"] = [hex(ida_bytes.get_dword(base + 4 * i)) if ida_bytes.is_loaded(base + 4 * i) else None for i in range(n)]
    w = {}
    for i in range(n):
        for x in idautils.XrefsTo(base + 4 * i):
            if x.type in (idc.dr_W,) or idc.print_insn_mnem(x.frm) == "mov" and idc.get_operand_value(x.frm, 0) == base + 4 * i:
                f = ida_funcs.get_func(x.frm)
                w.setdefault(hex(f.start_ea) if f else "none", []).append([i, hex(x.frm), idc.generate_disasm_line(x.frm, 0)])
    rep["writers"] = w
    for fs in list(w)[:4]:
        if fs != "none": dump(int(fs, 16))
run(main)
