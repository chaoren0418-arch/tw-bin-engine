# T1242 (#1242): read-only IDAPython. Decompile the first N slots of a named vftable (REA_VTBL, e.g. "XmlImage::`vftable'")
# and every function that reads the XmlImage type field this[70] (offset 0x118) via a [reg+118h] operand.
import os, traceback
from pathlib import Path
import ida_auto, ida_bytes, ida_funcs, ida_hexrays, ida_lines, ida_name, idautils, idc

out = Path(os.environ["REA_OUT"]); out.mkdir(parents=True, exist_ok=True)
ida_auto.auto_wait()
log = []


def dec(ea):
    try:
        c = ida_hexrays.decompile(ea)
        return "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
    except Exception as e:
        return "//ERR %s" % e


try:
    want = os.environ.get("REA_VTBL", "")
    vt = idc.BADADDR
    for ea, name in idautils.Names():
        dn = idc.demangle_name(name, idc.get_inf_attr(idc.INF_SHORT_DN)) or name
        if dn == want or name == want:
            vt = ea; break
    log.append("vtbl %s at %#x" % (want, vt))
    funcs = []
    if vt != idc.BADADDR:
        for i in range(int(os.environ.get("REA_N", "24"))):
            f = ida_bytes.get_dword(vt + 4 * i)
            log.append("slot %d (+%d) -> %#x" % (i, 4 * i, f))
            if ida_funcs.get_func(f):
                funcs.append(f)
    for v in filter(None, os.environ.get("REA_EXTRA", "").replace(",", " ").split()):
        funcs.append(int(v, 16))
    for f in funcs:
        fn = ida_funcs.get_func(f)
        if fn:
            (out / ("%08X.c" % fn.start_ea)).write_text(dec(fn.start_ea), encoding="utf-8")
except Exception:
    log.append(traceback.format_exc())
(out / "log.txt").write_text("\n".join(log), encoding="utf-8")
idc.qexit(0)
