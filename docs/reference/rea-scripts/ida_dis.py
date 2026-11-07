"""IDAPython (#431): mixed decompile/disassemble dump from a scratch IDB copy. Read-only.

REA_LIST lines:  dec VA            -> <VA>.c   (Hex-Rays pseudocode + callers in report.json)
                 dis START END     -> dis_<START>.asm (instructions in [START, END))
"""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto

out = Path(os.environ["REA_OUT"])
rep = {"functions": {}, "status": "?"}
try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    for line in Path(os.environ["REA_LIST"]).read_text().splitlines():
        p = line.split()
        if not p:
            continue
        if p[0] == "dec":
            f = ida_funcs.get_func(int(p[1], 16))
            r = {"start": hex(f.start_ea) if f else None}
            if f:
                r["callers"] = sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if x.iscode})[:40]
                try:
                    c = ida_hexrays.decompile(f.start_ea)
                    (out / f"{f.start_ea:08X}.c").write_text(
                        "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode()), encoding="utf-8")
                except Exception as e:
                    r["error"] = str(e)
            rep["functions"][p[1]] = r
        elif p[0] == "dis":
            a, b = int(p[1], 16), int(p[2], 16)
            rows = []
            ea = a
            while ea < b and ea != idc.BADADDR:
                rows.append(f"{ea:08X}  {idc.generate_disasm_line(ea, 0)}")
                ea = idc.next_head(ea, b)
            (out / f"dis_{a:08X}.asm").write_text("\n".join(rows), encoding="utf-8")
        elif p[0] == "xdec":
            # list every reference (code or data) to ADDR and decompile each referencing function
            t = int(p[1], 16)
            refs = []
            for x in idautils.XrefsTo(t):
                f = ida_funcs.get_func(x.frm)
                refs.append({"from": hex(x.frm), "func": hex(f.start_ea) if f else None, "code": bool(x.iscode)})
                if f and not (out / f"{f.start_ea:08X}.c").exists():
                    try:
                        c = ida_hexrays.decompile(f.start_ea)
                        (out / f"{f.start_ea:08X}.c").write_text(
                            "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode()), encoding="utf-8")
                    except Exception as e:
                        (out / f"{f.start_ea:08X}.err").write_text(str(e), encoding="utf-8")
            rep.setdefault("xrefs", {})[p[1]] = refs
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"
    rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
