"""T227 (#534) read-only: list.spr (TW13081901.txt) census of ']' '{' '}' frame tokens.
Usage: py -3 -I token_census.py <TW13081901.txt> <out.json>"""
import collections, json, pathlib, re, sys

text = pathlib.Path(sys.argv[1]).read_bytes().decode("cp950", "replace")
cur, name = None, ""
rows = collections.defaultdict(list)
for line in text.splitlines():
    m = re.match(r"#(\d+)\s+\d+\s+(.*)", line)
    if m:
        cur, name = int(m.group(1)), m.group(2).strip()[:40]
        continue
    for tok, val in re.findall(r"([\]\{\}])(-?\d+)", line):
        rows[tok].append((cur, name, int(val)))
out = {}
for tok, r in rows.items():
    sprites = sorted({(s, n) for s, n, _ in r})
    vals = collections.Counter(v for _, _, v in r)
    out[tok] = dict(occurrences=len(r), sprites=len(sprites), values=vals.most_common(30),
                    sample=[f"{s}({n})->{v}" for s, n, v in r[:40]])
pathlib.Path(sys.argv[2]).write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
for tok, o in out.items():
    print(tok, o["occurrences"], o["sprites"], o["values"][:12])
    print("  ", "; ".join(o["sample"][:25]))
