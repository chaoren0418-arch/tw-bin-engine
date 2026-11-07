"""IDAPython (read-only): all xrefs (with type) for given addresses; decompile writer functions. REA_LIST json {"addrs":[...], "decompile_writers":true}"""
import json, os, traceback, gc
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto
out = Path(os.environ["REA_OUT"]); q = json.loads(Path(os.environ["REA_LIST"]).read_text(encoding="utf-8-sig"))
rep = {"xrefs": {}, "functions": {}}
def dec(ea):
    f = ida_funcs.get_func(ea)
    if not f: return None
    try:
        c = ida_hexrays.decompile(f.start_ea)
        (out / f"{f.start_ea:08X}.c").write_text("\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode()), encoding="utf-8")
        return hex(f.start_ea)
    except Exception as e:
        return "err:" + str(e)
try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin": obj.term()
    ida_auto.auto_wait(); assert ida_hexrays.init_hexrays_plugin()
    for a in q["addrs"]:
        lst = []
        for x in idautils.XrefsTo(int(a, 16)):
            f = ida_funcs.get_func(x.frm)
            lst.append({"from": hex(x.frm), "func": hex(f.start_ea) if f else None, "type": x.type, "insn": idc.generate_disasm_line(x.frm, 0)})
        rep["xrefs"][a] = lst
        if q.get("decompile_writers"):
            for e in lst:
                if e["type"] == 2 and e["func"] and e["func"] not in rep["functions"]:
                    rep["functions"][e["func"]] = dec(int(e["func"], 16))
    for v in q.get("decompile", []):
        rep["functions"][v] = dec(int(v, 16))
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"; rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0)
