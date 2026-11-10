# T896 (#896): read-only IDAPython queries on the LiTo (3.81) OEP snapshot. Base: t742/ida_t742.py.
# Differences: the snapshot has one unnamed RWX section (code+data), so code/data scans walk every
# segment except Themida/.rsrc/.idata.  Extra queries:
#   ctx VA [N]        -> every code xref to VA with N (default 10) instructions before / 4 after
#   imm VALUE         -> every instruction whose operand value equals VALUE exactly (push/mov/cmp)
#   findstr TEXT      -> also scans the unnamed section
#   findbytes HEX     -> raw byte pattern search, with xrefs to each hit
#   range LO HI       -> decompile every function starting in [LO,HI) to <out>/<VA>.c
import gc, json, os, re, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto, ida_bytes, ida_name, ida_ua, ida_segment

out = Path(os.environ["REA_OUT"])
qs = [l.split() for l in Path(os.environ["REA_LIST"]).read_text(encoding="utf-8").splitlines() if l.strip() and not l.startswith("#")]
rep = {"q": []}
_dec = {}
SKIP = {"Themida", ".rsrc", ".idata"}


def segs():
    for s in idautils.Segments():
        if idc.get_segm_name(s) not in SKIP:
            yield s, idc.get_segm_end(s)


def fname(ea):
    f = ida_funcs.get_func(ea)
    return hex(f.start_ea) if f else None


def dis(ea):
    return ida_lines.tag_remove(idc.generate_disasm_line(ea, 0) or "")


def dectext(start):
    if start not in _dec:
        try:
            c = ida_hexrays.decompile(start)
            _dec[start] = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
        except Exception as e:
            _dec[start] = "//ERR " + str(e)
    return _dec[start]


def dec(va):
    f = ida_funcs.get_func(va)
    if not f:
        return {"va": hex(va), "error": "nofunc"}
    t = dectext(f.start_ea)
    (out / f"{f.start_ea:08X}.c").write_text(t, encoding="utf-8")
    return {"va": hex(va), "start": hex(f.start_ea), "end": hex(f.end_ea), "lines": t.count("\n") + 1,
            "callers": sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if x.iscode})[:80],
            "data_refs": sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if not x.iscode})[:20]}


def heads():
    for s, e in segs():
        for h in idautils.Heads(s, e):
            if ida_bytes.is_code(ida_bytes.get_flags(h)):
                yield h


def ctx(va, n):
    res = []
    for x in idautils.XrefsTo(va):
        ea = x.frm; pre = []
        for _ in range(n):
            ea = idc.prev_head(ea); pre.insert(0, f"{ea:08X} {dis(ea)}")
        post = []; ea = x.frm
        for _ in range(5):
            post.append(f"{ea:08X} {dis(ea)}"); ea = idc.next_head(ea, ea + 64)
        res.append({"frm": hex(x.frm), "func": fname(x.frm), "code": x.iscode, "asm": pre + post})
    return res[:120]


try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    hr = ida_hexrays.init_hexrays_plugin()
    rep["hexrays"] = bool(hr)
    rep["nfuncs"] = ida_funcs.get_func_qty()
    for q in qs:
        k = q[0]
        try:
            if k == "dec":
                rep["q"].append(dec(int(q[1], 16)))
            elif k == "xref":
                va = int(q[1], 16)
                rep["q"].append({"xref": hex(va), "refs": [(hex(x.frm), fname(x.frm), x.iscode) for x in idautils.XrefsTo(va)][:200]})
            elif k == "ctx":
                rep["q"].append({"ctx": q[1], "hits": ctx(int(q[1], 16), int(q[2]) if len(q) > 2 else 10)})
            elif k == "dis":
                va, n = int(q[1], 16), int(q[2]); ea = va; lines = []
                for _ in range(n):
                    lines.append(f"{ea:08X} {dis(ea)}"); ea = idc.next_head(ea, ea + 64)
                rep["q"].append({"dis": hex(va), "lines": lines})
            elif k == "fdis":
                f = ida_funcs.get_func(int(q[1], 16))
                lines = [f"{h:08X} {dis(h)}" for h in idautils.Heads(f.start_ea, f.end_ea)]
                (out / f"{f.start_ea:08X}.asm").write_text("\n".join(lines), encoding="utf-8")
                rep["q"].append({"fdis": hex(f.start_ea), "n": len(lines)})
            elif k == "imm":
                v = int(q[1], 16); res = []; insn = ida_ua.insn_t()
                for h in heads():
                    if ida_ua.decode_insn(insn, h) <= 0:
                        continue
                    for op in insn.ops:
                        if op.type == ida_ua.o_void:
                            break
                        if op.type == ida_ua.o_imm and (op.value & 0xFFFFFFFF) == v:
                            res.append([hex(h), fname(h), dis(h)]); break
                rep["q"].append({"imm": q[1], "n": len(res), "hits": res[:400]})
            elif k == "findstr" or k == "findbytes":
                pat = (" ".join(q[1:]).encode("latin-1") + b"\0") if k == "findstr" else bytes.fromhex("".join(q[1:]))
                res = []
                for s, e in segs():
                    blob = ida_bytes.get_bytes(s, e - s) or b""; i = 0
                    while True:
                        i = blob.find(pat, i)
                        if i < 0:
                            break
                        ea = s + i; i += 1; st = ea
                        if k == "findstr":
                            while st > s and ida_bytes.get_byte(st - 1) != 0:
                                st -= 1
                        res.append({"ea": hex(ea), "start": hex(st), "refs": [(hex(x.frm), fname(x.frm)) for x in idautils.XrefsTo(st)][:40]})
                rep["q"].append({k: " ".join(q[1:]), "hits": res[:100]})
            elif k == "grepfuncs":
                rx = re.compile(q[1]); res = {}
                for v in q[2:]:
                    f = ida_funcs.get_func(int(v, 16))
                    if not f:
                        continue
                    t = dectext(f.start_ea); (out / f"{f.start_ea:08X}.c").write_text(t, encoding="utf-8")
                    res[hex(f.start_ea)] = [l.strip() for l in t.splitlines() if rx.search(l)][:40]
                rep["q"].append({"grepfuncs": q[1], "hits": res})
            elif k == "grepcallers":
                va = int(q[1], 16); rx = re.compile(" ".join(q[2:])); res = {}
                starts = sorted({ida_funcs.get_func(x.frm).start_ea for x in idautils.XrefsTo(va) if x.iscode and ida_funcs.get_func(x.frm)})
                for s in starts:
                    hits = [l.strip() for l in dectext(s).splitlines() if rx.search(l)]
                    if hits:
                        res[hex(s)] = hits[:20]
                rep["q"].append({"grepcallers": q[1], "n": len(starts), "hits": res})
            elif k == "dwords":
                va, n = int(q[1], 16), int(q[2])
                rep["q"].append({"dwords": q[1], "v": [hex(ida_bytes.get_dword(va + 4 * i)) for i in range(n)]})
            elif k == "range":
                lo, hi = int(q[1], 16), int(q[2], 16); done = []
                f = ida_funcs.get_next_func(lo - 1)
                while f and f.start_ea < hi:
                    t = dectext(f.start_ea); (out / f"{f.start_ea:08X}.c").write_text(t, encoding="utf-8")
                    done.append(hex(f.start_ea)); f = ida_funcs.get_next_func(f.start_ea)
                rep["q"].append({"range": q[1:3], "funcs": done})
            elif k == "mkfunc":
                a = int(q[1], 16); ida_ua.create_insn(a); ok = ida_funcs.add_func(a); ida_auto.auto_wait()
                r = dec(a); r["mkfunc_ok"] = bool(ok); rep["q"].append(r)
        except Exception:
            rep["q"].append({"q": q, "error": traceback.format_exc()})
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"; rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1, ensure_ascii=False), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)

