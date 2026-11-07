"""Find instructions writing [reg+DISP] (ARG='disp_hex[,palette_hex]') across all functions; report functions
that also reference the palette address. Output fieldw.json."""
import sys, os, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, idc, ida_funcs, ida_ua

def main(rep):
    parts = ARG.split(",")
    disp = int(parts[0], 16)
    pal = int(parts[1], 16) if len(parts) > 1 else None
    hits = {}
    for fea in idautils.Functions():
        f = ida_funcs.get_func(fea)
        for h in idautils.Heads(f.start_ea, f.end_ea):
            m = idc.print_insn_mnem(h)
            if m not in ("mov",): continue
            if idc.get_operand_type(h, 0) == idc.o_displ and idc.get_operand_value(h, 0) == disp:
                insn = ida_ua.insn_t(); ida_ua.decode_insn(insn, h)
                if insn.ops[0].dtype == ida_ua.dt_word:
                    hits.setdefault(hex(fea), []).append([hex(h), idc.generate_disasm_line(h, 0)])
    out = {}
    for fs, lst in hits.items():
        f = ida_funcs.get_func(int(fs, 16))
        refpal = False
        if pal:
            for h in idautils.Heads(f.start_ea, f.end_ea):
                for op in range(2):
                    v = idc.get_operand_value(h, op)
                    if pal <= v < pal + 0x200: refpal = True
        out[fs] = {"writes": lst, "refpal": refpal}
    (OUT / "fieldw.json").write_text(json.dumps(out, indent=1), encoding="utf-8")
    rep["n"] = len(out); rep["refpal"] = [k for k, v in out.items() if v["refpal"]]
run(main)
