"""IDAPython read-only query. REA_LIST = JSON with keys: strings, xrefs, decompile, callers_depth, exec (python file)."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, ida_bytes, idautils, idc, ida_auto, ida_ida, ida_name

out = Path(os.environ["REA_OUT"])
q = json.loads(Path(os.environ["REA_LIST"]).read_text(encoding="utf-8"))
rep = {"strings": {}, "xrefs": {}, "functions": {}, "log": []}

def fstart(ea):
    f = ida_funcs.get_func(ea)
    return f.start_ea if f else None

def xrefs(ea, n=120):
    res = []
    for x in idautils.XrefsTo(ea):
        fs = fstart(x.frm)
        res.append({"from": hex(x.frm), "func": hex(fs) if fs else None, "type": x.type})
    return res[:n]

def find_all(bs, n=40):
    hits, ea = [], ida_ida.inf_get_min_ea()
    end = ida_ida.inf_get_max_ea()
    while len(hits) < n:
        ea = ida_bytes.bin_search(ea, end, bs, None, ida_bytes.BIN_SEARCH_FORWARD, ida_bytes.BIN_SEARCH_CASE)
        if ea == idc.BADADDR:
            break
        hits.append(ea); ea += 1
    return hits

def decomp(va):
    f = ida_funcs.get_func(va)
    r = {"start": hex(f.start_ea) if f else None, "size": f.size() if f else 0}
    if f:
        r["callers"] = sorted({hex(fstart(x.frm) or 0) + "@" + hex(x.frm) for x in idautils.XrefsTo(f.start_ea)})[:80]
        p = out / f"{f.start_ea:08X}.c"
        if p.exists():
            return r
        try:
            c = ida_hexrays.decompile(f.start_ea)
            text = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
            p.write_text(text, encoding="utf-8")
            r["lines"] = text.count("\n") + 1
        except Exception as e:
            r["error"] = str(e)
    return r

def log(*a):
    rep["log"].append(" ".join(str(x) for x in a))

try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    for s in q.get("strings", []):
        bs = s.encode("latin1")
        rep["strings"][s] = [{"ea": hex(h), "xrefs": xrefs(h)} for h in find_all(bs)]
    for v in q.get("xrefs", []):
        rep["xrefs"][v] = xrefs(int(v, 16))
    auto = set()
    if q.get("decompile_string_users"):
        for s, hits in rep["strings"].items():
            for h in hits:
                for x in h["xrefs"]:
                    if x["func"]: auto.add(x["func"])
    for v in list(q.get("decompile", [])) + sorted(auto):
        if v not in rep["functions"]:
            rep["functions"][v] = decomp(int(v, 16))
    if q.get("exec"):
        src = Path(os.environ["REA_ROOT"], q["exec"]).read_text(encoding="utf-8-sig")
        exec(compile(src, q["exec"], "exec"), globals())
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"; rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
