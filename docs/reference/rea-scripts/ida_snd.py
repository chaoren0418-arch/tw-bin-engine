"""IDAPython (#431): sound-path survey on a scratch IDB copy. Read-only, no patching.

Emits to REA_OUT:
  imports.json   - Miles (AIL_*) / winmm / dsound imports and the functions that call them
  strings.json   - sound/music related strings and referencing functions
  callers.json   - callers of REA_LIST seed functions (2 levels)
  <VA>.c         - Hex-Rays pseudocode for seeds, callers, import users, string users
"""
import gc, json, os, re, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, ida_nalt, idautils, idc, ida_auto, ida_bytes

out = Path(os.environ["REA_OUT"])
seeds = [int(x, 16) for x in Path(os.environ["REA_LIST"]).read_text().split()]
rep = {"status": "?"}
done = set()


def dec(ea):
    f = ida_funcs.get_func(ea)
    if not f or f.start_ea in done:
        return f.start_ea if f else None
    done.add(f.start_ea)
    try:
        c = ida_hexrays.decompile(f.start_ea)
        text = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
        (out / f"{f.start_ea:08X}.c").write_text(text, encoding="utf-8")
    except Exception as e:
        (out / f"{f.start_ea:08X}.err").write_text(str(e), encoding="utf-8")
    return f.start_ea


def fstart(ea):
    f = ida_funcs.get_func(ea)
    return f.start_ea if f else None


try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()

    imps = {}
    pat = re.compile(r"AIL_|waveOut|mci|PlaySound|DirectSound|midiOut|sndPlay", re.I)
    for i in range(ida_nalt.get_import_module_qty()):
        mod = ida_nalt.get_import_module_name(i)

        def cb(ea, name, ord_, mod=mod):
            if name and pat.search(name):
                users = sorted({fstart(x.frm) for x in idautils.XrefsTo(ea) if fstart(x.frm)})
                imps[f"{mod}!{name}"] = {"iat": hex(ea), "users": [hex(u) for u in users]}
                for u in users:
                    dec(u)
            return True
        ida_nalt.enum_import_names(i, cb)
    (out / "imports.json").write_text(json.dumps(imps, indent=1), encoding="utf-8")

    strs = {}
    spat = re.compile(r"sound|music|\.wav|\.mid|\.mp3|\.ogg|bgm|miles|mss|volume|effect\.", re.I)
    for s in idautils.Strings():
        t = str(s)
        if spat.search(t):
            users = sorted({fstart(x.frm) for x in idautils.XrefsTo(s.ea) if fstart(x.frm)})
            strs[hex(s.ea)] = {"s": t[:120], "users": [hex(u) for u in users]}
            for u in users[:6]:
                dec(u)
    (out / "strings.json").write_text(json.dumps(strs, indent=1, ensure_ascii=False), encoding="utf-8")

    callers = {}
    for sd in seeds:
        dec(sd)
        lvl1 = sorted({fstart(x.frm) for x in idautils.XrefsTo(sd) if fstart(x.frm)})
        callers[hex(sd)] = {"callers": [hex(c) for c in lvl1], "sites": sorted(hex(x.frm) for x in idautils.XrefsTo(sd))}
        for c in lvl1:
            dec(c)
    (out / "callers.json").write_text(json.dumps(callers, indent=1), encoding="utf-8")
    rep["decompiled"] = len(done)
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"
    rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
