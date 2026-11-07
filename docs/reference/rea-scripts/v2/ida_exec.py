"""IDAPython read-only runner: exec the user snippet at REA_LIST with helpers; output to REA_OUT."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, ida_bytes, idautils, idc, ida_auto, ida_ida, ida_ua, ida_name

out = Path(os.environ["REA_OUT"])
log = []

def P(*a):
    log.append(" ".join(str(x) for x in a))

def fstart(ea):
    f = ida_funcs.get_func(ea)
    return f.start_ea if f else None

def xr(ea):
    return [(x.frm, fstart(x.frm), x.type) for x in idautils.XrefsTo(ea)]

def dec(va, save=True):
    f = ida_funcs.get_func(va)
    if not f:
        return None
    try:
        c = ida_hexrays.decompile(f.start_ea)
        text = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
        if save:
            (out / f"{f.start_ea:08X}.c").write_text(text, encoding="utf-8")
        return text
    except Exception as e:
        return "ERR " + str(e)

def find_all(bs, limit=50):
    hits, ea = [], ida_ida.inf_get_min_ea()
    end = ida_ida.inf_get_max_ea()
    while len(hits) < limit:
        ea = ida_bytes.bin_search(ea, end, bs, None, ida_bytes.BIN_SEARCH_FORWARD, ida_bytes.BIN_SEARCH_CASE)
        if ea == idc.BADADDR:
            break
        hits.append(ea); ea += 1
    return hits

def dis(ea, n=40):
    r = []
    for _ in range(n):
        r.append(f"{ea:08X}  {idc.GetDisasm(ea)}")
        ea = idc.next_head(ea, ida_ida.inf_get_max_ea())
    return "\n".join(r)

try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    exec(Path(os.environ["REA_LIST"]).read_text(encoding="utf-8-sig"), globals())
    status = "pass"
except Exception:
    status = "fail"; P(traceback.format_exc())
(out / "log.txt").write_text(status + "\n" + "\n".join(log), encoding="utf-8")
idc.qexit(0)

