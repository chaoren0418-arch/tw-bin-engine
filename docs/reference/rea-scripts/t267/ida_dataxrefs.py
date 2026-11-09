"""IDAPython (#628): list every xref into data ranges. REA_LIST lines: START END. Read-only."""
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
    if len(p) != 2:
        continue
    a, b = int(p[0], 16), int(p[1], 16)
    rows = []
    for ea in range(a, b):
        for x in idautils.XrefsTo(ea):
            f = ida_funcs.get_func(x.frm)
            rows.append({"to": hex(ea), "name": idc.get_name(ea), "from": hex(x.frm),
                         "func": hex(f.start_ea) if f else None, "type": x.type,
                         "dis": idc.generate_disasm_line(x.frm, 0)})
    rep[p[0]] = rows
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0)
