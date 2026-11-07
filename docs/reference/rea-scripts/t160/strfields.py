"""List small functions in [A30000,A60000) that write dword [reg+disp] for disp in ARG list; decompile them + callers."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, idc, ida_funcs, ida_ua

def main(rep):
    disps = [int(x, 16) for x in ARG.split(",")]
    res = {}
    for fea in idautils.Functions(0xA30000, 0xA60000):
        f = ida_funcs.get_func(fea)
        if f.size() > 0x300: continue
        for h in idautils.Heads(f.start_ea, f.end_ea):
            if idc.print_insn_mnem(h) != "mov": continue
            if idc.get_operand_type(h, 0) == idc.o_displ and idc.get_operand_value(h, 0) in disps:
                insn = ida_ua.insn_t(); ida_ua.decode_insn(insn, h)
                if insn.ops[0].dtype == ida_ua.dt_dword:
                    d = hex(idc.get_operand_value(h, 0))
                    res.setdefault(d, {})[hex(fea)] = [hex(c) for c in callers(fea)][:12]
                    dump(fea)
    rep["res"] = res
run(main)
