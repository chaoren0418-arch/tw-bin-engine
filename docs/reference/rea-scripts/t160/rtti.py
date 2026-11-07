"""Map MSVC RTTI type names -> vtables -> vfuncs; note sends (0x9FBC00) inside first-level vfuncs.
Output rtti.json: [{name, td, col, vtable, vfuncs:[...], sends:[[fmt,op,site]...]}]"""
import sys, os, re, json; sys.path.insert(0, os.path.dirname(__file__))
from common import *
import idautils, ida_funcs, idc, ida_bytes, ida_segment

SEND = 0x9FBC00
FILTER = re.compile(ARG or ".", re.I)

def sends_in(fea):
    f = ida_funcs.get_func(fea)
    if not f: return []
    out = []
    for h in idautils.Heads(f.start_ea, f.end_ea):
        if idc.print_insn_mnem(h) == "call" and idc.get_operand_value(h, 0) == SEND:
            pushes = []; ea = h
            for _ in range(30):
                ea = idc.prev_head(ea, f.start_ea)
                if ea == idc.BADADDR or ea < f.start_ea: break
                if idc.print_insn_mnem(ea) == "push":
                    pushes.append(ea)
                    if len(pushes) >= 3: break
            fmt = op = arg = None
            if len(pushes) >= 2:
                s = idc.get_strlit_contents(idc.get_operand_value(pushes[0], 0))
                fmt = s.decode("latin1") if s else idc.print_operand(pushes[0], 0)
                op = idc.print_operand(pushes[1], 0)
                if len(pushes) >= 3: arg = idc.print_operand(pushes[2], 0)
            out.append([fmt, op, arg, hex(h)])
    return out

def main(rep):
    res = []
    seg = ida_segment.get_first_seg()
    pat = b".?AV"
    while seg:
        ea = seg.start_ea
        while True:
            ea = ida_bytes.find_bytes(pat, ea, range_end=seg.end_ea) if hasattr(ida_bytes, "find_bytes") else idc.find_binary(ea, idc.SEARCH_DOWN, '2E 3F 41 56')
            if ea == idc.BADADDR or ea >= seg.end_ea: break
            name = idc.get_strlit_contents(ea, -1, idc.STRTYPE_C)
            name = name.decode("latin1") if name else "?"
            td = ea - 8
            if FILTER.search(name):
                for c in idautils.DataRefsTo(td):
                    col = c - 12
                    if ida_bytes.get_dword(col) != 0: continue
                    for v in idautils.DataRefsTo(col):
                        vt = v + 4
                        vf = []
                        for i in range(12):
                            p = ida_bytes.get_dword(vt + 4 * i)
                            fn = ida_funcs.get_func(p)
                            if not fn or fn.start_ea != p: break
                            if i and list(idautils.DataRefsTo(vt + 4 * i)): break
                            vf.append(hex(p))
                        snd = []
                        for p in vf[:4]: snd += sends_in(int(p, 16))
                        res.append({"name": name, "td": hex(td), "col": hex(col), "vtable": hex(vt), "vfuncs": vf, "sends": snd})
            ea += 4
        seg = ida_segment.get_next_seg(seg.start_ea)
    (OUT / "rtti.json").write_text(json.dumps(res, indent=0), encoding="utf-8")
    rep["count"] = len(res)
run(main)
