"""IDAPython query (read-only): find strings -> xrefs -> functions; data xrefs; decompile. Input JSON at REA_LIST."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, ida_bytes, idautils, idc, ida_auto, ida_ida

out = Path(os.environ["REA_OUT"])
q = json.loads(Path(os.environ["REA_LIST"]).read_text(encoding="utf-8-sig"))
rep = {"strings": {}, "xrefs": {}, "functions": {}}

def fstart(ea):
    f = ida_funcs.get_func(ea)
    return f.start_ea if f else None

def xrefs(ea):
    res = []
    for x in idautils.XrefsTo(ea):
        fs = fstart(x.frm)
        res.append({"from": hex(x.frm), "func": hex(fs) if fs else None, "type": x.type})
    return res[:80]

def find_all(bs):
    hits, ea = [], ida_ida.inf_get_min_ea()
    end = ida_ida.inf_get_max_ea()
    while len(hits) < 30:
        ea = ida_bytes.bin_search(ea, end, bs, None, ida_bytes.BIN_SEARCH_FORWARD, ida_bytes.BIN_SEARCH_CASE)
        if ea == idc.BADADDR:
            break
        hits.append(ea); ea += 1
    return hits

def decomp(va):
    f = ida_funcs.get_func(va)
    r = {"start": hex(f.start_ea) if f else None, "size": f.size() if f else 0}
    if f:
        r["callers"] = sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if x.iscode})[:60]
        try:
            c = ida_hexrays.decompile(f.start_ea)
            text = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
            (out / f"{f.start_ea:08X}.c").write_text(text, encoding="utf-8")
            r["lines"] = text.count("\n") + 1
        except Exception as e:
            r["error"] = str(e)
    return r

try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    for s in q.get("strings", []):
        bs = s.encode("latin1") if isinstance(s, str) else bytes(s)
        rep["strings"][s] = [{"ea": hex(h), "xrefs": xrefs(h)} for h in find_all(bs)]
    for v in q.get("xrefs", []):
        rep["xrefs"][v] = xrefs(int(v, 16))
    for lo, hi in q.get("ranges", []):
        for fs in idautils.Functions(int(lo, 16), int(hi, 16)):
            rep["functions"].setdefault(hex(fs), decomp(fs))
    if q.get("rtti"):
        # MSVC RTTI: TypeDescriptor = name - 8; COL+0xC -> TD; vtable[-1] -> COL; code refs to vtable = ctor/dtor
        rep["rtti"] = {}
        for nm in q["rtti"]:
            hits = find_all(nm.encode("latin1") + b"\0")
            r = []
            for h in hits:
                td = h - 8
                for x in idautils.XrefsTo(td):
                    col = x.frm - 12
                    for y in idautils.XrefsTo(col):
                        vt = y.frm + 4
                        users = []
                        for z in idautils.XrefsTo(vt):
                            fs = fstart(z.frm)
                            if fs is None: continue
                            callers = sorted({hex(fstart(c.frm) or 0) for c in idautils.XrefsTo(fs) if c.iscode})[:20]
                            users.append({"func": hex(fs), "callers": callers})
                        r.append({"td": hex(td), "col": hex(col), "vtable": hex(vt), "users": users[:20]})
            rep["rtti"][nm] = r
    auto = set()
    if q.get("decompile_string_users"):
        for s, hits in rep["strings"].items():
            for h in hits:
                for x in h["xrefs"]:
                    if x["func"]: auto.add(x["func"])
    for v in list(q.get("decompile", [])) + sorted(auto):
        if v not in rep["functions"]:
            rep["functions"][v] = decomp(int(v, 16))
    if q.get("imm_scan"):
        import ida_ua, ida_idp
        want = set(q["imm_scan"]["values"]); need = q["imm_scan"]["min"]
        hits = []
        for fs in idautils.Functions():
            fn = ida_funcs.get_func(fs)
            if fn.size() > 30000: continue
            seen = set()
            for ea in idautils.FuncItems(fs):
                if idc.print_insn_mnem(ea) != "cmp": continue
                if idc.get_operand_type(ea, 1) == idc.o_imm:
                    v = idc.get_operand_value(ea, 1) & 0xFF
                    if idc.get_operand_value(ea, 1) in want or (v in want and idc.get_operand_value(ea, 1) < 0x100):
                        seen.add(v)
            if len(seen) >= need:
                hits.append({"func": hex(fs), "vals": sorted(seen)})
        rep["imm_scan"] = hits
        for h in hits[:q["imm_scan"].get("decompile", 0)]:
            rep["functions"][h["func"]] = decomp(int(h["func"], 16))
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"; rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
