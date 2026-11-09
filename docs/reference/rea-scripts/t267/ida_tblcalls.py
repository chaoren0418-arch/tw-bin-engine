"""IDAPython (#628): for every call to the given functions, print the 8 instructions before it (read-only).
REA_LIST lines: FUNC_VA"""
import json, os, gc
from pathlib import Path
import ida_funcs, idautils, idc, ida_auto

out = Path(os.environ["REA_OUT"])
rep = {}
for obj in gc.get_objects():
    if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
        obj.term()
ida_auto.auto_wait()
for line in Path(os.environ["REA_LIST"]).read_text().splitlines():
    p = line.split()
    if not p:
        continue
    t = int(p[0], 16)
    sites = []
    for x in idautils.XrefsTo(t):
        if not x.iscode:
            continue
        f = ida_funcs.get_func(x.frm)
        prev, ea = [], x.frm
        for _ in range(8):
            ea = idc.prev_head(ea)
            prev.insert(0, f"{ea:08X}  {idc.generate_disasm_line(ea, 0)}")
        sites.append({"site": hex(x.frm), "func": hex(f.start_ea) if f else None, "before": prev})
    rep[p[0]] = sites
(out / "report.json").write_text(json.dumps(rep, indent=1, ensure_ascii=False), encoding="utf-8")
idc.qexit(0)
