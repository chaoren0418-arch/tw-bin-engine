"""Common helpers for IDAPython batch scripts (read-only; never saves IDB changes)."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto, ida_bytes

OUT = Path(os.environ["REA_OUT"])
ARG = os.environ.get("REA_ARG", "")

def boot():
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()

def dec(ea):
    f = ida_funcs.get_func(ea)
    if not f: return None
    try:
        c = ida_hexrays.decompile(f.start_ea)
        return "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
    except Exception as e:
        return "// decompile error: %s" % e

def dump(ea, tag=""):
    f = ida_funcs.get_func(ea)
    if not f: return
    t = dec(f.start_ea)
    (OUT / ("%08X%s.c" % (f.start_ea, tag))).write_text(t or "", encoding="utf-8")

def callers(ea):
    return sorted({x.frm for x in idautils.XrefsTo(ea) if x.iscode})

def run(main):
    rep = {}
    try:
        boot(); main(rep); rep["status"] = "pass"
    except Exception:
        rep["status"] = "fail"; rep["exc"] = traceback.format_exc()
    (OUT / "report.json").write_text(json.dumps(rep, indent=1, default=str), encoding="utf-8")
    idc.qexit(0)
