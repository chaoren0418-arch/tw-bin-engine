"""IDAPython (read-only): decompile listed VAs; 'D:<va>' lists and decompiles functions referencing data <va>.
Writes <func>.c pseudocode and report.json into REA_OUT. Never saves the IDB (qexit without save is not used; -A batch on a scratch copy)."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto

out = Path(os.environ["REA_OUT"])
items = Path(os.environ["REA_LIST"]).read_text().split()
rep = {"functions": {}, "data": {}}

def dec(ea):
    f = ida_funcs.get_func(ea)
    r = {"start": hex(f.start_ea) if f else None, "size": f.size() if f else 0}
    if not f:
        return r
    r["callers"] = sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if x.iscode})[:40]
    p = out / f"{f.start_ea:08X}.c"
    if not p.exists():
        try:
            c = ida_hexrays.decompile(f.start_ea)
            p.write_text("\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode()), encoding="utf-8")
        except Exception as e:
            r["error"] = str(e)
    return r

try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    for it in items:
        if it.startswith("D:"):
            va = int(it[2:], 16)
            fs = {}
            for x in idautils.XrefsTo(va):
                f = ida_funcs.get_func(x.frm)
                fs.setdefault(hex(f.start_ea) if f else "nofunc", []).append(hex(x.frm))
            rep["data"][hex(va)] = fs
            for k in list(fs)[:25]:
                if k != "nofunc":
                    rep["functions"][k] = dec(int(k, 16))
        else:
            rep["functions"][it] = dec(int(it, 16))
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"
    rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
