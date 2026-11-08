"""T244 (#601): recount gap-matrix.md after the round-2 delta (section 11). Read-only.
Base rows = sections 1-5 (same rule as gap_count.py). Section 11's delta table
(| 節 | 列 | 原狀態 | 新狀態 | ... |) overrides the status of the matching base row:
`列` must equal the row's first cell, or be a unique prefix of it, within that section.
原狀態 `新增` adds a row. 原狀態 must match the base row, so a stale delta fails loudly.
Usage: py -3 -I docs/reference/rea-scripts/gap_count_r2.py docs/reference/gap-matrix.md
"""
import re, sys, pathlib
sys.stdout.reconfigure(encoding="utf-8")
text = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8")
STATES = ("已有", "部分", "缺")


def cells(row):
    return [c.strip() for c in row.strip().strip("|").split("|")]


def kind(state, row):
    if state != "缺":
        return state
    if "需先查證" in row:
        return "缺·需先查證"
    if "伺服器不支援" in row or "不建議" in row:
        return "缺·伺服器不支援或不建議"
    return "缺·可直接補"


base, titles, delta = {}, {}, None
for sec in re.split(r"\n## ", text):
    m = re.match(r"(\d+)\. (.*)", sec)
    if not m:
        continue
    n = int(m.group(1))
    if 1 <= n <= 5:
        titles[n] = m.group(2).split("（")[0]
        rows = base.setdefault(n, [])
        for row in sec.splitlines():
            if not row.startswith("|") or row.startswith("|---"):
                continue
            hits = re.findall(r"\| (已有|部分|缺) \|", row)
            if len(hits) == 1:
                rows.append({"key": cells(row)[0], "state": hits[0], "kind": kind(hits[0], row)})
    elif n == 11:
        delta = sec
if delta is None:
    sys.exit("section 11 (round 2) not found")

errors, applied = [], 0
for row in delta.splitlines():
    if not row.startswith("|") or row.startswith("|---"):
        continue
    c = cells(row)
    if len(c) < 6 or not re.fullmatch(r"\d(\.\d)?", c[0]) or c[3] not in STATES:
        continue
    n, key, old, new = int(c[0][0]), c[1], c[2], c[3]
    rows = base[n]
    if old == "新增":
        rows.append({"key": key, "state": new, "kind": kind(new, row)})
        applied += 1
        continue
    hit = [r for r in rows if r["key"] == key] or [r for r in rows if r["key"].startswith(key)]
    if len(hit) != 1:
        errors.append(f"§{c[0]} {key}: {len(hit)} matches")
        continue
    if hit[0]["state"] != old:
        errors.append(f"§{c[0]} {key}: base is {hit[0]['state']}, delta says {old}")
        continue
    hit[0]["state"], hit[0]["kind"] = new, kind(new, row)
    applied += 1
if errors:
    sys.exit("\n".join(errors))

keys = ["已有", "部分", "缺", "缺·需先查證", "缺·伺服器不支援或不建議", "缺·可直接補"]
total = dict.fromkeys(keys, 0)
print(f"套用第二輪變更 {applied} 列")
print("| 節 | 已有 | 部分 | 缺 | 其中需先查證 | 其中伺服器不支援／不建議 | 其中可直接補 |")
print("|---|---:|---:|---:|---:|---:|---:|")
for n in sorted(base):
    c = dict.fromkeys(keys, 0)
    for r in base[n]:
        c[r["state"]] += 1
        if r["state"] == "缺":
            c[r["kind"]] += 1
    for k in keys:
        total[k] += c[k]
    print(f"| {n}. {titles[n]} | {c['已有']} | {c['部分']} | {c['缺']} | {c['缺·需先查證']} | {c['缺·伺服器不支援或不建議']} | {c['缺·可直接補']} |")
print(f"| **合計** | **{total['已有']}** | **{total['部分']}** | **{total['缺']}** | {total['缺·需先查證']} | {total['缺·伺服器不支援或不建議']} | {total['缺·可直接補']} |")
