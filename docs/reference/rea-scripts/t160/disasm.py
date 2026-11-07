"""Linear disassembly of range ARG='start,end' (creates code if undefined, in the scratch copy only)."""
import sys, os; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idc, ida_ua, ida_bytes, ida_funcs, idautils

def main(rep):
    a, b = [int(x, 16) for x in ARG.split(",")]
    lines = []
    ea = a
    while ea < b:
        if not ida_bytes.is_code(ida_bytes.get_flags(ea)):
            ida_ua.create_insn(ea)
        n = idc.get_item_size(ea) or 1
        txt = idc.generate_disasm_line(ea, 0) or ""
        extra = ""
        for op in range(2):
            v = idc.get_operand_value(ea, op)
            if idc.get_operand_type(ea, op) in (idc.o_imm, idc.o_mem) and v > 0x1000000:
                s = idc.get_strlit_contents(v)
                if s: extra += "  ; str=" + s.decode("cp950", "replace")
        f = ida_funcs.get_func(ea)
        lines.append("%08X %s%s%s" % (ea, "" if f else "! ", txt, extra))
        ea += n
    (OUT / ("dis_%X.asm" % a)).write_text("\n".join(lines), encoding="utf-8")
    rep["n"] = len(lines)
run(main)
