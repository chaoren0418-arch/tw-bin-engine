"""Survey: (1) every call to the packet sender 0x9FBC00 with fmt/opcode; (2) strings matching keywords + xrefs."""
import sys, os, re; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, ida_funcs, idc, ida_ua

SEND = 0x9FBC00
KW = re.compile(r"emblem|title|pledge|clan|duel|pink|name|emot|chat|\.bmp|%s|font|blood|poly|action", re.I)

def push_args(call_ea, n=4):
    """Walk back from call collecting push operands (raw text) - nearest first."""
    out = []; ea = call_ea
    f = ida_funcs.get_func(call_ea)
    for _ in range(40):
        ea = idc.prev_head(ea, f.start_ea if f else 0)
        if ea == idc.BADADDR: break
        m = idc.print_insn_mnem(ea)
        if m == "call": break
        if m == "push":
            out.append((hex(ea), idc.print_operand(ea, 0), idc.get_operand_value(ea, 0)))
            if len(out) >= n: break
    return out

def main(rep):
    sites = []
    for x in idautils.XrefsTo(SEND):
        if not x.iscode: continue
        args = push_args(x.frm, 40)
        # cdecl: last pushed before call = first arg (fmt), then opcode
        fmt = None; op = None
        if len(args) >= 2:
            s = idc.get_strlit_contents(args[0][2])
            fmt = s.decode("latin1") if s else args[0][1]
            op = args[1][1]
        f = ida_funcs.get_func(x.frm)
        sites.append({"site": hex(x.frm), "func": hex(f.start_ea) if f else None, "fmt": fmt, "op": op})
    rep["send_sites"] = sites
    strs = []
    for s in idautils.Strings():
        try: t = str(s)
        except Exception: continue
        if KW.search(t) and len(t) < 120:
            xs = sorted({hex(ida_funcs.get_func(x.frm).start_ea) if ida_funcs.get_func(x.frm) else hex(x.frm) for x in idautils.XrefsTo(s.ea)})
            strs.append({"ea": hex(s.ea), "s": t, "xref_funcs": xs[:12]})
    rep["strings"] = strs
run(main)
