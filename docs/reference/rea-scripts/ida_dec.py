"""IDAPython: decompile listed VAs from a scratch IDB copy; emit pseudocode + callers. No patching."""
import gc, json, os, sys, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, ida_name, ida_xref, idautils, idc, ida_auto

out = Path(os.environ["REA_OUT"])
targets = [int(x, 16) for x in Path(os.environ["REA_LIST"]).read_text().split()]
rep = {"plugins_disabled": [], "functions": {}}
try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
            rep["plugins_disabled"].append(type(obj).__name__)
    ida_auto.auto_wait()
    rep["function_count"] = len(list(idautils.Functions()))
    assert ida_hexrays.init_hexrays_plugin()
    for va in targets:
        f = ida_funcs.get_func(va)
        r = {"start": hex(f.start_ea) if f else None, "size": f.size() if f else 0}
        if f:
            r["callers"] = sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if x.iscode})[:40]
            r["data_refs"] = sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if not x.iscode})[:20]
            try:
                c = ida_hexrays.decompile(f.start_ea)
                text = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
                (out / f"{f.start_ea:08X}.c").write_text(text, encoding="utf-8")
                r["lines"] = text.count("\n") + 1
            except Exception as e:
                r["error"] = str(e)
        rep["functions"][hex(va)] = r
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"
    rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
