"""T629 (#629): IDAPython read-only queries. REA_LIST lines: 'dec VA' | 'vt SLOTVA' | 'xref VA' | 'dis VA N'."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto, ida_bytes, ida_name

out = Path(os.environ["REA_OUT"])
qs = [l.split() for l in Path(os.environ["REA_LIST"]).read_text().splitlines() if l.strip() and not l.startswith("#")]
rep = {"q": []}

def fname(ea):
    f = ida_funcs.get_func(ea)
    return hex(f.start_ea) if f else None

def dec(va):
    f = ida_funcs.get_func(va)
    if not f:
        return {"va": hex(va), "error": "nofunc"}
    r = {"va": hex(va), "start": hex(f.start_ea),
         "callers": sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if x.iscode})[:60],
         "data_refs": sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if not x.iscode})[:20]}
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
    for q in qs:
        kind, va = q[0], int(q[1], 16)
        if kind == "dec":
            rep["q"].append(dec(va))
        elif kind == "vt":
            # walk back from slot to vtable start (first address with an xref)
            s = va
            while s > va - 0x400 and not list(idautils.XrefsTo(s)):
                s -= 4
            refs = [(hex(x.frm), fname(x.frm)) for x in idautils.XrefsTo(s)]
            col = idc.get_wide_dword(s - 4)
            rep["q"].append({"slot": hex(va), "vt": hex(s), "index": (va - s) // 4, "refs": refs, "col": hex(col),
                             "name": ida_name.get_name(s)})
        elif kind == "xref":
            rep["q"].append({"va": hex(va), "refs": [(hex(x.frm), fname(x.frm), x.iscode) for x in idautils.XrefsTo(va)][:80]})
        elif kind == "calls":
            f = ida_funcs.get_func(va); res = []
            for x in idautils.XrefsTo(f.start_ea):
                if not x.iscode: continue
                ea = x.frm; prev = []
                p = ea
                for _ in range(4):
                    p = idc.prev_head(p, p - 32); prev.append(ida_lines.tag_remove(idc.generate_disasm_line(p, 0) or ""))
                res.append({"site": hex(ea), "func": fname(ea), "prev": prev[::-1]})
            rep["q"].append({"calls": hex(va), "sites": res})
        elif kind == "vtdump":
            n = int(q[2]); ents = []
            for i in range(n):
                fe = idc.get_wide_dword(va + 4 * i)
                ents.append(hex(fe))
                if ida_funcs.get_func(fe): dec(fe)
            rep["q"].append({"vtdump": hex(va), "ents": ents})
        elif kind == "dis":
            n = int(q[2]); ea = va; lines = []
            for _ in range(n):
                lines.append(f"{ea:08X} " + ida_lines.tag_remove(idc.generate_disasm_line(ea, 0) or ""))
                ea = idc.next_head(ea, ea + 64)
            rep["q"].append({"dis": hex(va), "lines": lines})
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"; rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
