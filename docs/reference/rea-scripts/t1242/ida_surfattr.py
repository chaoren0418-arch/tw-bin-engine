# T1242 (#1242): read-only IDAPython. Find the XML attribute strings BaseSurf / HighlightSurf / PushSurf / Surf and
# decompile every function that references them (and their direct callees one level down), so the meaning of the
# "id;N" suffix of surf attributes can be read. Also decompiles the extra VAs listed in REA_EXTRA (hex, comma separated).
import os
from pathlib import Path
import ida_auto, ida_bytes, ida_funcs, ida_hexrays, ida_lines, idautils, idc

out = Path(os.environ["REA_OUT"])
out.mkdir(parents=True, exist_ok=True)
ida_auto.auto_wait()
import traceback
try:
    exec(compile(r"""targets = [b"BaseSurf\0", b"HighlightSurf\0", b"PushSurf\0", b"Surf\0", b"%d.img\0", b"%dc.img\0", b"%s.img\0"]
log = []
funcs = set()
import re
seg_lo, seg_hi = idc.get_inf_attr(idc.INF_MIN_EA), idc.get_inf_attr(idc.INF_MAX_EA)
for t in targets:
    pat = " ".join("%02X" % b for b in t)
    ea = seg_lo
    while True:
        ea = ida_bytes.find_bytes(pat, ea, None, seg_hi) if hasattr(ida_bytes, "find_bytes") else idc.find_binary(ea, idc.SEARCH_DOWN | idc.SEARCH_NEXT, pat)
        if ea is None or ea == idc.BADADDR or ea >= seg_hi:
            break
        if ida_bytes.get_byte(ea - 1) == 0:  # whole string, not the tail of a longer one
            refs = [x.frm for x in idautils.XrefsTo(ea)]
            log.append("str %s VA %#x refs %s" % (t[:-1].decode(), ea, [hex(r) for r in refs][:40]))
            for r in refs:
                f = ida_funcs.get_func(r)
                if f:
                    funcs.add(f.start_ea)
        ea += 1
for v in filter(None, re.split(r"[,\s]+", os.environ.get("REA_EXTRA", ""))):
    f = ida_funcs.get_func(int(v, 16))
    if f:
        funcs.add(f.start_ea)


def dec(ea):
    try:
        c = ida_hexrays.decompile(ea)
        return "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
    except Exception as e:
        return "//ERR %s" % e


for ea in sorted(funcs)[:60]:
    (out / ("%08X.c" % ea)).write_text(dec(ea), encoding="utf-8")
    log.append("dec %#x" % ea)
(out / "log.txt").write_text("\n".join(log), encoding="utf-8")

""", "body", "exec"))
except Exception:
    (out / "error.txt").write_text(traceback.format_exc(), encoding="utf-8")
idc.qexit(0)
