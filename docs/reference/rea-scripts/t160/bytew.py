"""Functions writing byte [reg+DISP] (ARG='disp_hex,lo_hex,hi_hex'); decompile up to 12 and list callers."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, idc, ida_funcs, ida_ua

def main(rep):
    d, lo, hi = [int(x, 16) for x in ARG.split(",")]
    res = {}
    for fea in idautils.Functions(lo, hi):
        f = ida_funcs.get_func(fea)
        for h in idautils.Heads(f.start_ea, f.end_ea):
            if idc.print_insn_mnem(h) != "mov": continue
            if idc.get_operand_type(h, 0) == idc.o_displ and idc.get_operand_value(h, 0) == d:
                insn = ida_ua.insn_t(); ida_ua.decode_insn(insn, h)
                if insn.ops[0].dtype == ida_ua.dt_byte:
                    res.setdefault(hex(fea), []).append(idc.generate_disasm_line(h, 0))
    rep["res"] = {k: {"w": v, "callers": [hex(c) for c in callers(int(k, 16))][:10]} for k, v in res.items()}
    for k in list(res)[:12]: dump(int(k, 16))
run(main)
