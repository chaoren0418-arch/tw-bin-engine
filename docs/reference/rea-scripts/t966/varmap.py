"""T422 (#966): read-only. (1) parse the IDA pseudocode of sub_8676F0 <var> switch -> id: condition + string ids;
(2) scan extracted *-c.html for <var src="#N"> (N>=998) with the preceding <a action/link> label;
(3) print string-c.tbl ids. Usage: py -3 -I varmap.py DEC_C TEXT_DIR LTTEXT_DIR OUT_JSON"""
import json, re, sys, pathlib, collections

dec, tdir, ltdir, outp = sys.argv[1:5]
lines = pathlib.Path(dec).read_text(encoding="utf-8").splitlines()

# --- (1) BIN side: inside the var branch (from 'case 999:' to the 'case 14:' of the tag switch)
start = next(i for i, l in enumerate(lines) if l.strip() == "case 999:")
end = next(i for i, l in enumerate(lines) if i > start and l.strip() == "case 14:" and l.startswith("      case"))
cases = collections.OrderedDict()
cur = None
for l in lines[start:end]:
    m = re.match(r"\s*case (\d+)u?:", l)
    if m and int(m.group(1)) >= 998:
        cur = int(m.group(1)); cases[cur] = {"cond": None, "strs": [], "body": []}
        continue
    if cur is None:
        continue
    s = l.strip()
    c = cases[cur]
    if len(c["body"]) < 14:
        c["body"].append(s)
    if c["cond"] is None and (s.startswith("if (") or s.startswith("v") or s.startswith("for (") or s.startswith("switch")):
        c["cond"] = s
    for sid in re.findall(r"sub_4EAFF0\((\d+)\)", s):
        if int(sid) not in c["strs"]:
            c["strs"].append(int(sid))
# 2001..2016 share one tail: itoa(byte)
# --- (3) string table
st = (pathlib.Path(tdir) / "string-c.tbl").read_bytes().decode("cp950", errors="replace").replace("\r", "").split("\n")
def S(i):
    return st[i + 1] if 0 <= i + 1 < len(st) else None
# --- (2) html side
rx = re.compile(r'<var src="#(\d+)">', re.I)
html = collections.defaultdict(list)
for d in (tdir, ltdir):
    for p in sorted(pathlib.Path(d).glob("*-c.html")):
        t = p.read_bytes().decode("cp950", errors="replace")
        for m in rx.finditer(t):
            n = int(m.group(1))
            if n < 998:
                continue
            seg = t[max(0, m.start() - 300):m.start()]
            a = list(re.finditer(r'<a (action|link)="([^"]+)"[^>]*>([^<]*)</a>', seg))
            lab = (a[-1].group(2), a[-1].group(3).strip()) if a else (None, re.sub(r"<[^>]+>", "", seg.split("<br>")[-1]).strip())
            html[n].append({"page": p.name, "action": lab[0], "label": lab[1]})
out = {}
for n in sorted(set(cases) | set(html)):
    c = cases.get(n, {})
    out[n] = {"cond": c.get("cond"), "strs": {s: S(s) for s in c.get("strs", [])},
              "html": html.get(n, [])[:4], "pages": sorted({h["page"] for h in html.get(n, [])})}
pathlib.Path(outp).write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
for n, v in out.items():
    h = v["html"][0] if v["html"] else {}
    print(n, "|", v["cond"], "|", ",".join(f"{k}={s}" for k, s in v["strs"].items()), "|", h.get("action"), h.get("label"), "|", len(v["pages"]))
