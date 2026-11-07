"""T180 (#482): for receive opcodes in ARG (comma list), find the case block of sub_947530's first dispatch group
(index 0x94828C, targets 0x9480C0, #417) and list the calls made in that block; decompile the called handlers."""
import sys, os; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idc, ida_bytes

IDX, TGT = 0x94828C, 0x9480C0

def main(rep):
    starts = set()
    for op in range(255):
        i = ida_bytes.get_byte(IDX + op)
        if i <= 114:
            starts.add(ida_bytes.get_dword(TGT + 4 * i))
    starts = sorted(starts)
    for op in [int(x) for x in ARG.split(",")]:
        i = ida_bytes.get_byte(IDX + op)
        if i > 114:
            rep[op] = "default"; continue
        blk = ida_bytes.get_dword(TGT + 4 * i)
        nxt = min([s for s in starts if s > blk] + [blk + 0x80])
        calls, ea = [], blk
        while ea < nxt and len(calls) < 8:
            if idc.print_insn_mnem(ea) == "call":
                calls.append(hex(idc.get_operand_value(ea, 0)))
            ea = idc.next_head(ea, nxt + 1)
        rep[op] = {"block": hex(blk), "calls": calls}
        for c in calls[:3]:
            dump(int(c, 16))
run(main)
