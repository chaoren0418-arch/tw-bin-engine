"""B04 quantification: event tokens of map-effect gfx ids in TW13081901.txt; LiTo parser window check."""
import collections, json, pathlib, re, sys
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

txt = pathlib.Path(r"D:\lineage開發\0-客戶端\TW13081901.txt").read_bytes().replace(b"\r\r\n", b"\n").replace(b"\r\n", b"\n")
s = txt.decode("cp950", "replace")
print("head", repr(s[:160]))
starts = [(int(m.group(1)), m.start()) for m in re.finditer(r"(?m)^#(\d+)\b", s)]
print("blocks", len(starts), starts[:3])
blocks = {}
for i, (g, p) in enumerate(starts):
    blocks[g] = s[p:starts[i + 1][1] if i + 1 < len(starts) else len(s)]
print("6016 block:", repr(blocks.get(6016, "")[:400]))
census = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
ids = dict(census["ids"])
tok = re.compile(r"\d+\.\d+:\d+((?:[\[<^]\d+)+)")
sym = collections.Counter()
recs = collections.Counter()
per = {}
missing = 0
for g, n in ids.items():
    g = int(g)
    b = blocks.get(g)
    if b is None:
        missing += 1
        continue
    found = collections.Counter()
    for m in re.finditer(r"([\[<^])(\d+)", b):
        found[m.group(1)] += 1
    if found:
        per[g] = dict(found)
        for k in found:
            sym[k] += 1
            recs[k] += n
print("effect ids", len(ids), "no block", missing, "ids with tokens", len(per))
print("ids per symbol", dict(sym), "records per symbol", dict(recs))
print("sample", list(per.items())[:12])
pathlib.Path(sys.argv[2]).write_text(json.dumps(per), encoding="utf-8")

print("== LiTo window")
b = pathlib.Path(r"D:\lineage開發\3-lito開發\lito登入器\舊版分析與驗收\runtime\LiTo.unpacked-oep-current.exe").read_bytes()
md = Cs(CS_ARCH_X86, CS_MODE_32)
md.skipdata = True
for c in (0x3E85C, 0xE9701):
    lo = max(0, c - 0x1800)
    cnt = collections.Counter()
    for a, sz, m, o in md.disasm_lite(b[lo:c + 0x1800], lo):
        if m == "cmp":
            for k, v in {"<": "0x3c", "[": "0x5b", "^": "0x5e", "!": "0x21", ">": "0x3e", "]": "0x5d", "{": "0x7b", "}": "0x7d"}.items():
                if o.endswith(", " + v):
                    cnt[k] += 1
        if m == "xor" and re.search(r"0x(1951|2b|1812)$", o):
            cnt["xor " + o.split(", ")[1]] += 1
        if m == "imul" and "0x24" in o:
            cnt["imul24"] += 1
    print(hex(c), dict(cnt))
