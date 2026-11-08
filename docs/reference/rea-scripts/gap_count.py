"""T202 (#504): recount gap-matrix.md status cells per section (sections 1-5). Read-only.
A row counts when it has exactly one `| 已有 |`, `| 部分 |` or `| 缺 |` cell. A 缺 row is sub-classified by its text:
"需先查證" first, then "伺服器不支援" / "不建議".
Usage: py -3 -I docs/reference/rea-scripts/gap_count.py docs/reference/gap-matrix.md
"""
import re, sys, pathlib
sys.stdout.reconfigure(encoding="utf-8")
text = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8")
total = {"已有": 0, "部分": 0, "缺": 0, "缺·需先查證": 0, "缺·伺服器不支援或不建議": 0, "缺·可直接補": 0}
print("| 節 | 已有 | 部分 | 缺 | 其中需先查證 | 其中伺服器不支援／不建議 | 其中可直接補 |")
print("|---|---:|---:|---:|---:|---:|---:|")
for sec in re.split(r"\n## ", text):
    m = re.match(r"(\d+)\. (.*)", sec)
    if not m or not 1 <= int(m.group(1)) <= 5:
        continue
    c = dict.fromkeys(total, 0)
    for row in sec.splitlines():
        if not row.startswith("|") or row.startswith("|---"):
            continue
        hits = re.findall(r"\| (已有|部分|缺) \|", row)
        if len(hits) != 1:
            continue
        s = hits[0]
        c[s] += 1
        if s == "缺":
            k = "缺·需先查證" if "需先查證" in row else "缺·伺服器不支援或不建議" if ("伺服器不支援" in row or "不建議" in row) else "缺·可直接補"
            c[k] += 1
    for k in total:
        total[k] += c[k]
    print(f"| {m.group(1)}. {m.group(2).split('（')[0]} | {c['已有']} | {c['部分']} | {c['缺']} | {c['缺·需先查證']} | {c['缺·伺服器不支援或不建議']} | {c['缺·可直接補']} |")
print(f"| **合計** | **{total['已有']}** | **{total['部分']}** | **{total['缺']}** | {total['缺·需先查證']} | {total['缺·伺服器不支援或不建議']} | {total['缺·可直接補']} |")
