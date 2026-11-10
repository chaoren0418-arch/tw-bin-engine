# T742 (#742): ida_t690.py + rangegrep LO HI REGEX (decompile every function in [LO,HI), save .c, keep matching lines). Read-only.
"""T690 (#690): ida_t680.py + mkfunc A B / vtab VA N / dwords VA N. Base T680 (#680): IDAPython read-only queries (extends ida_t268.py).
REA_LIST lines:
  dec VA | xref VA | dis VA N | str VA
  setter OFF MAXSIZE        -> small funcs that store to [reg+OFF] (byte/dword)
  stores OFF                -> every instruction storing to [reg+OFF] (mov/inc/dec/or/and), whole program
  immscan LO HI             -> every operand value / displacement in [LO,HI] (whole program, code + data dwords)
  grepcallers VA REGEX      -> decompile callers of VA, keep lines matching REGEX
  grepfuncs REGEX VA...     -> decompile listed funcs, keep lines matching REGEX
"""
import gc, json, os, re, traceback
from pathlib import Path
import ida_funcs, ida_hexrays, ida_lines, idautils, idc, ida_auto, ida_bytes, ida_name, ida_ua, ida_segment

out = Path(os.environ["REA_OUT"])
qs = [l.split() for l in Path(os.environ["REA_LIST"]).read_text().splitlines() if l.strip() and not l.startswith("#")]
rep = {"q": []}
_dec_cache = {}


def fname(ea):
    f = ida_funcs.get_func(ea)
    return hex(f.start_ea) if f else None


def dectext(start):
    if start in _dec_cache:
        return _dec_cache[start]
    try:
        c = ida_hexrays.decompile(start)
        t = "\n".join(ida_lines.tag_remove(l.line) for l in c.get_pseudocode())
    except Exception as e:
        t = "//ERR " + str(e)
    _dec_cache[start] = t
    return t


def dec(va):
    f = ida_funcs.get_func(va)
    if not f:
        return {"va": hex(va), "error": "nofunc"}
    r = {"va": hex(va), "start": hex(f.start_ea),
         "callers": sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if x.iscode})[:80],
         "data_refs": sorted({hex(x.frm) for x in idautils.XrefsTo(f.start_ea) if not x.iscode})[:20]}
    t = dectext(f.start_ea)
    (out / f"{f.start_ea:08X}.c").write_text(t, encoding="utf-8")
    r["lines"] = t.count("\n") + 1
    return r


def text_heads():
    for s in idautils.Segments():
        seg = ida_segment.getseg(s)
        if seg.perm & ida_segment.SEGPERM_EXEC or idc.get_segm_name(s) == ".text":
            for h in idautils.Heads(seg.start_ea, seg.end_ea):
                if ida_bytes.is_code(ida_bytes.get_flags(h)):
                    yield h


STORE_MN = {"mov", "inc", "dec", "or", "and", "xor", "add", "sub", "setnz", "setz", "setne", "sete", "movzx"}


def stores(off, maxsize=None):
    res = []
    insn = ida_ua.insn_t()
    for h in text_heads():
        if ida_ua.decode_insn(insn, h) <= 0:
            continue
        op0 = insn.ops[0]
        if op0.type in (ida_ua.o_displ, ida_ua.o_phrase) and op0.addr == off:
            mn = insn.get_canon_mnem()
            if mn in STORE_MN and mn != "movzx":
                f = ida_funcs.get_func(h)
                if maxsize is not None and (not f or f.end_ea - f.start_ea > maxsize):
                    continue
                res.append([hex(h), fname(h), ida_lines.tag_remove(idc.generate_disasm_line(h, 0) or "")])
    return res


def immscan(lo, hi):
    res = []
    insn = ida_ua.insn_t()
    for h in text_heads():
        if ida_ua.decode_insn(insn, h) <= 0:
            continue
        for op in insn.ops:
            if op.type == ida_ua.o_void:
                break
            for v in (op.value, op.addr):
                if lo <= (v & 0xFFFFFFFF) <= hi:
                    res.append([hex(h), fname(h), ida_lines.tag_remove(idc.generate_disasm_line(h, 0) or "")])
                    break
    # data dwords pointing into range
    for s in idautils.Segments():
        if idc.get_segm_name(s) in (".rdata", ".data"):
            e = idc.get_segm_end(s)
            ea = s
            while ea + 4 <= e:
                v = ida_bytes.get_dword(ea)
                if lo <= v <= hi:
                    res.append([hex(ea), "data", hex(v)])
                ea += 4
    return res


try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    assert ida_hexrays.init_hexrays_plugin()
    for q in qs:
        kind = q[0]
        if kind == "dec":
            rep["q"].append(dec(int(q[1], 16)))
        elif kind == "xref":
            va = int(q[1], 16)
            rep["q"].append({"va": hex(va), "refs": [(hex(x.frm), fname(x.frm), x.iscode) for x in idautils.XrefsTo(va)][:200]})
        elif kind == "dis":
            va, n = int(q[1], 16), int(q[2]); ea = va; lines = []
            for _ in range(n):
                lines.append(f"{ea:08X} " + ida_lines.tag_remove(idc.generate_disasm_line(ea, 0) or ""))
                ea = idc.next_head(ea, ea + 64)
            rep["q"].append({"dis": hex(va), "lines": lines})
        elif kind == "str":
            va = int(q[1], 16)
            b = ida_bytes.get_bytes(va, 128) or b""
            rep["q"].append({"str": hex(va), "s": b.split(b"\0")[0].decode("latin-1")})
        elif kind == "setter":
            rep["q"].append({"setter": q[1], "hits": stores(int(q[1], 16), int(q[2], 16))})
        elif kind == "stores":
            rep["q"].append({"stores": q[1], "hits": stores(int(q[1], 16))})
        elif kind == "immscan":
            rep["q"].append({"immscan": q[1:3], "hits": immscan(int(q[1], 16), int(q[2], 16))})
        elif kind == "grepcallers":
            va = int(q[1], 16); rx = re.compile(" ".join(q[2:])); res = {}
            starts = sorted({ida_funcs.get_func(x.frm).start_ea for x in idautils.XrefsTo(va) if x.iscode and ida_funcs.get_func(x.frm)})
            for s in starts:
                hits = [l.strip() for l in dectext(s).splitlines() if rx.search(l)]
                if hits:
                    res[hex(s)] = hits[:20]
            rep["q"].append({"grepcallers": q[1], "rx": rx.pattern, "n": len(starts), "hits": res})
        elif kind == "grepfuncs":
            rx = re.compile(q[1]); res = {}
            for v in q[2:]:
                f = ida_funcs.get_func(int(v, 16))
                if not f:
                    continue
                t = dectext(f.start_ea)
                (out / f"{f.start_ea:08X}.c").write_text(t, encoding="utf-8")
                res[hex(f.start_ea)] = [l.strip() for l in t.splitlines() if rx.search(l)][:40]
            rep["q"].append({"grepfuncs": q[1], "hits": res})
        elif kind == "findstr":
            pat = " ".join(q[1:]).encode("latin-1"); res = []
            for s in idautils.Segments():
                if idc.get_segm_name(s) not in (".rdata", ".data", ".image"):
                    continue
                e = idc.get_segm_end(s); blob = ida_bytes.get_bytes(s, e - s) or b""; k = 0
                while True:
                    k = blob.find(pat + b"\0", k)
                    if k < 0:
                        break
                    ea = s + k; k += 1; st = ea
                    while st > s and ida_bytes.get_byte(st - 1) != 0:
                        st -= 1
                    res.append({"ea": hex(ea), "start": hex(st), "refs": [(hex(x.frm), fname(x.frm)) for x in idautils.XrefsTo(st)][:40]})
            rep["q"].append({"findstr": pat.decode("latin-1"), "hits": res})
        elif kind == "mkfunc":
            a, b = int(q[1], 16), int(q[2], 16); ea = a
            while ea < b:
                ida_ua.create_insn(ea); ea += idc.get_item_size(ea) or 1
            ok = ida_funcs.add_func(a, b); ida_auto.auto_wait()
            r = dec(a); r["mkfunc_ok"] = bool(ok); rep["q"].append(r)
        elif kind == "vtab":
            va, n = int(q[1], 16), int(q[2]); rows = []
            for i in range(n):
                v = ida_bytes.get_dword(va + 4 * i); rows.append([hex(va + 4 * i), "+%d" % (4 * i), hex(v), ida_name.get_name(v)])
            rep["q"].append({"vtab": q[1], "rows": rows})
        elif kind == "dwords":
            va, n = int(q[1], 16), int(q[2])
            rep["q"].append({"dwords": q[1], "v": [hex(ida_bytes.get_dword(va + 4 * i)) for i in range(n)]})
        elif kind == "fdis":
            f = ida_funcs.get_func(int(q[1], 16)); lines = []
            for h in idautils.Heads(f.start_ea, f.end_ea):
                lines.append(f"{h:08X} " + ida_lines.tag_remove(idc.generate_disasm_line(h, 0) or ""))
            (out / f"{f.start_ea:08X}.asm").write_text("\n".join(lines), encoding="utf-8")
            rep["q"].append({"fdis": hex(f.start_ea), "n": len(lines)})
        elif kind == "nameea":
            ea = ida_name.get_name_ea(idc.BADADDR, q[1])
            rep["q"].append({"nameea": q[1], "ea": hex(ea), "refs": [(hex(x.frm), fname(x.frm)) for x in idautils.XrefsTo(ea)][:60]})
        elif kind == "rangegrep":
            lo, hi = int(q[1], 16), int(q[2], 16); rx = re.compile(" ".join(q[3:])); res = {}
            f = ida_funcs.get_next_func(lo - 1)
            while f and f.start_ea < hi:
                t = dectext(f.start_ea)
                (out / f"{f.start_ea:08X}.c").write_text(t, encoding="utf-8")
                hits = [l.strip() for l in t.splitlines() if rx.search(l)]
                if hits:
                    res[hex(f.start_ea)] = hits[:30]
                f = ida_funcs.get_next_func(f.start_ea)
            rep["q"].append({"rangegrep": q[1:3], "rx": rx.pattern, "hits": res})
        elif kind == "gstores":
            tgt = int(q[1], 16); res = []
            for x in idautils.XrefsTo(tgt):
                insn = ida_ua.insn_t()
                if ida_ua.decode_insn(insn, x.frm) > 0 and insn.ops[0].type == ida_ua.o_mem and insn.ops[0].addr == tgt and insn.get_canon_mnem() == "mov":
                    prev = []; ea = x.frm
                    for _ in range(6):
                        ea = idc.prev_head(ea); prev.insert(0, f"{ea:08X} " + ida_lines.tag_remove(idc.generate_disasm_line(ea, 0) or ""))
                    res.append({"at": hex(x.frm), "func": fname(x.frm), "prev": prev})
            rep["q"].append({"gstores": q[1], "hits": res})
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"; rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)

