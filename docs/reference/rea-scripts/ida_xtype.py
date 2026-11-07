import json, os
from pathlib import Path
import ida_funcs, idautils, idc, ida_auto, ida_xref
out = Path(os.environ["REA_OUT"]); rep = {}
ida_auto.auto_wait()
for it in Path(os.environ["REA_LIST"]).read_text().split():
    va = int(it, 16); rows = []
    for x in idautils.XrefsTo(va):
        f = ida_funcs.get_func(x.frm)
        rows.append([hex(x.frm), hex(f.start_ea) if f else None, {ida_xref.dr_W: "W", ida_xref.dr_R: "R", ida_xref.dr_O: "O"}.get(x.type, str(x.type))])
    rep[hex(va)] = rows
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0)