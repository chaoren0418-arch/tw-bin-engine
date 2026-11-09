"""IDAPython (T266 / #627): read-only batch query against the scratch IDB copy (never saves the IDB).

REA_LIST lines:
  dec VA          -> <FUNC>.c (Hex-Rays pseudocode); report: callers
  dis START END   -> dis_<START>.asm
  xdec ADDR       -> decompile every function referencing ADDR; report: refs
  xlist ADDR      -> report only: every reference to ADDR with its containing function (no decompile)
  vt NAME|VA [N]  -> report: first N (default 40) code pointers of a vtable (by demangled/IDA name or VA); decompile each
  vtl NAME|VA [N] -> same as vt, list only
Run with run_ida.ps1 -Script ida_t266.py -List <file> -Out <dir> -Drive <free letter>.
"""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto, ida_bytes, ida_name

out = Path(os.environ["REA_OUT"])
rep = {"functions": {}, "refs": {}, "vt": {}, "status": "?"}


def fstart(ea):
    f = ida_funcs.get_func(ea)
    return f.start_ea if f else None


def dump(ea):
    s = fstart(ea)
    if s is None or (out / f"{s:08X}.c").exists():
        return
    try:
        c = ida_hexrays.decompile(s)
        t = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
    except Exception as e:  # keep going; record the error in the file
        t = f"// decompile error: {e}"
    (out / f"{s:08X}.c").write_text(t, encoding="utf-8")


def resolve(tok):
    try:
        return int(tok, 16)
    except ValueError:
        for ea, n in idautils.Names():
            if n == tok or (ida_name.demangle_name(n, 0) or "") == tok:
                return ea
        return idc.BADADDR


try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    for line in Path(os.environ["REA_LIST"]).read_text().splitlines():
        p = line.split()
        if not p or p[0].startswith("#"):
            continue
        cmd = p[0]
        if cmd == "dec":
            ea = int(p[1], 16)
            s = fstart(ea)
            rep["functions"][p[1]] = {
                "start": hex(s) if s is not None else None,
                "callers": sorted({hex(x.frm) for x in idautils.XrefsTo(s) if x.iscode})[:60] if s is not None else [],
                "data_refs": sorted({hex(x.frm) for x in idautils.XrefsTo(s) if not x.iscode})[:30] if s is not None else [],
            }
            dump(ea)
        elif cmd == "dis":
            a, b = int(p[1], 16), int(p[2], 16)
            rows, ea = [], a
            while ea < b and ea != idc.BADADDR:
                rows.append(f"{ea:08X}  {idc.generate_disasm_line(ea, 0)}")
                ea = idc.next_head(ea, b)
            (out / f"dis_{a:08X}.asm").write_text("\n".join(rows), encoding="utf-8")
        elif cmd in ("xdec", "xlist"):
            t = int(p[1], 16)
            refs = []
            for x in idautils.XrefsTo(t):
                s = fstart(x.frm)
                refs.append({"from": hex(x.frm), "func": hex(s) if s is not None else None, "code": bool(x.iscode)})
                if cmd == "xdec" and s is not None:
                    dump(s)
            rep["refs"][p[1]] = refs
        elif cmd in ("vt", "vtl"):
            vt = resolve(p[1])
            n = int(p[2]) if len(p) > 2 else 40
            ents = []
            for i in range(n):
                q = ida_bytes.get_dword(vt + 4 * i)
                if not ida_funcs.get_func(q):
                    break
                ents.append(hex(q))
                if cmd == "vt":
                    dump(q)
            rep["vt"][p[1]] = {"va": hex(vt), "entries": ents}
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"
    rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
