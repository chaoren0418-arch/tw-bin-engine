"""IDAPython (T245 #602, read-only): RTTI type descriptor -> COL -> vtable -> code users (+ their callers).
REA_LIST lines: <TD_VA> ; writes rtti.json and <func>.c into REA_OUT. Never saves the IDB."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto, ida_bytes, ida_search

out = Path(os.environ["REA_OUT"])
rep = {"status": "?", "rtti": {}}

def dec(ea):
    f = ida_funcs.get_func(ea)
    if not f:
        return None
    p = out / f"{f.start_ea:08X}.c"
    if not p.exists():
        try:
            c = ida_hexrays.decompile(f.start_ea)
            p.write_text("\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode()), encoding="utf-8")
        except Exception as e:
            (out / f"{f.start_ea:08X}.err").write_text(str(e), encoding="utf-8")
    return f.start_ea

def drefs(ea):
    return [x.frm for x in idautils.XrefsTo(ea)]

def scan_dword(val, lo=0x401000, hi=0x1800000):
    # brute search for a DWORD value in loaded segments (for refs IDA did not create)
    res = []
    pat = " ".join(f"{b:02X}" for b in val.to_bytes(4, "little"))
    ea = lo
    while True:
        ea = ida_search.find_binary(ea, hi, pat, 16, ida_search.SEARCH_DOWN)
        if ea == idc.BADADDR or ea >= hi:
            break
        res.append(ea)
        ea += 1
        if len(res) > 50:
            break
    return res

try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    for line in Path(os.environ["REA_LIST"]).read_text().split():
        td = int(line, 16)
        r = {"name": idc.get_strlit_contents(td + 8) and idc.get_strlit_contents(td + 8).decode("latin1"),
             "td_refs": [hex(x) for x in scan_dword(td)], "cols": []}
        for ref in scan_dword(td):
            col = ref - 12  # COL: sig, offset, cdOffset, pTypeDescriptor
            c = {"col": hex(col), "col_refs": []}
            for vr in scan_dword(col):
                vt = vr + 4
                users = []
                for x in idautils.XrefsTo(vt):
                    f = ida_funcs.get_func(x.frm)
                    fs = dec(x.frm) if f else None
                    callers = []
                    if fs:
                        for y in idautils.XrefsTo(fs):
                            g = ida_funcs.get_func(y.frm)
                            callers.append({"from": hex(y.frm), "func": hex(g.start_ea) if g else None})
                            if g:
                                dec(g.start_ea)
                    users.append({"from": hex(x.frm), "func": hex(fs) if fs else None, "callers": callers[:20]})
                c["col_refs"].append({"at": hex(vr), "vtable": hex(vt), "vt0": hex(ida_bytes.get_dword(vt)),
                                      "vt_refs_raw": [hex(z) for z in scan_dword(vt)], "users": users})
            r["cols"].append(c)
        rep["rtti"][line] = r
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"
    rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
