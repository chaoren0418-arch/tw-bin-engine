"""T427 (#980): recount gap-matrix.md after the round-2..9 deltas and check the round-9 classification (15.14).
Read-only. Same rules as gap_count_r8.py, except:
- Section 15 is split at "### 15.12", "### 15.13" and "### 15.14". Delta tables before 15.12 are round 6 (15.3),
  15.12 is round 7, 15.13 is round 8, 15.14 is round 9. All use | 節 | 列 | 原狀態 | 新狀態 | ... | and are applied
  in that order.
- The classification table is read from 15.14 only (15.13.4 and older are history). Labels are K<n>.
  K7 = 進行中: the row is counted with the state its open task is expected to reach (the 新狀態 in the delta table).
  A K7 item may name an 已有 row (expected 已有, actually 部分 until the task merges); the second table
  ("不算預期") moves those rows back to 部分.
Every non-已有 row must be classified exactly once; 缺 rows tagged 伺服器不支援／不建議 default to K1.
Usage: py -3 -I docs/reference/rea-scripts/gap_count_r9.py docs/reference/gap-matrix.md [--list]
"""
import re, sys, pathlib
sys.stdout.reconfigure(encoding="utf-8")
text = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8")
STATES = ("已有", "部分", "缺")
ROUNDS = (11, 12, 13, 14, 15, 16, 17, 18)  # 16 = 15.12 (R7), 17 = 15.13 (R8), 18 = 15.14 (R9)


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


def find(rows, key):
    return [r for r in rows if r["key"] == key] or [r for r in rows if r["key"].startswith(key)]


base, titles, deltas = {}, {}, {}
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
    elif n == 15:
        head, sep, rest = sec.partition("\n### 15.12")
        r7, sep2, rest = rest.partition("\n### 15.13")
        r8, sep3, r9 = rest.partition("\n### 15.14")
        if not (sep and sep2 and sep3):
            sys.exit("15.12, 15.13 or 15.14 not found")
        r9 = r9.partition("\n### 15.15")[0]  # T441: round 10 is counted by gap_count_r10.py
        deltas[15], deltas[16], deltas[17], deltas[18] = head, r7, r8, r9
    elif n in ROUNDS:
        deltas[n] = sec
if set(deltas) != set(ROUNDS):
    sys.exit("section 11-15 not found")

errors, applied = [], dict.fromkeys(ROUNDS, 0)
for rnd, row in [(r, x) for r in ROUNDS for x in deltas[r].splitlines()]:
    if not row.startswith("|") or row.startswith("|---"):
        continue
    c = cells(row)
    if len(c) < 6 or not re.fullmatch(r"\d(\.\d)?", c[0]) or c[3] not in STATES:
        continue
    n, key, old, new = int(c[0][0]), c[1], c[2], c[3]
    rows = base[n]
    if old == "新增":
        rows.append({"key": key, "state": new, "kind": kind(new, row)})
        applied[rnd] += 1
        continue
    hit = find(rows, key)
    if len(hit) != 1:
        errors.append(f"R{rnd - 9} §{c[0]} {key}: {len(hit)} matches")
        continue
    if hit[0]["state"] != old:
        errors.append(f"R{rnd - 9} §{c[0]} {key}: current is {hit[0]['state']}, delta says {old}")
        continue
    hit[0]["state"], hit[0]["kind"] = new, kind(new, row)
    applied[rnd] += 1

# Classification (15.14)
for row in deltas[18].splitlines():
    m = re.match(r"\| (K\d)[^|]*\| ([^|]+) \|", row)
    if not m or ":" not in m.group(2):
        continue
    label = m.group(1)
    for item in m.group(2).split("；"):
        item = re.sub(r"（[^）]*）$", "", item.strip())  # K7 items carry their task number: 4:WidgetSlot（#969）
        if not item:
            continue
        sec, _, key = item.partition(":")
        if not sec.isdigit() or int(sec) not in base:
            errors.append(f"K {item}: bad section")
            continue
        pool = base[int(sec)] if label == "K7" else [r for r in base[int(sec)] if r["state"] != "已有"]
        hit = find(pool, key)
        if len(hit) != 1:
            errors.append(f"{label} {item}: {len(hit)} matches")
            continue
        if "cat" in hit[0]:
            errors.append(f"{label} {item}: already in {hit[0]['cat']}")
            continue
        hit[0]["cat"] = label
for n in base:
    for r in base[n]:
        if r["state"] == "已有" or "cat" in r:
            continue
        if r["kind"] == "缺·伺服器不支援或不建議":
            r["cat"] = "K1"
        else:
            errors.append(f"unclassified §{n} {r['state']} {r['key']}")
if errors:
    sys.exit("\n".join(errors))

keys = ["已有", "部分", "缺", "缺·需先查證", "缺·伺服器不支援或不建議", "缺·可直接補"]


def table(actual):
    total = dict.fromkeys(keys, 0)
    print("| 節 | 已有 | 部分 | 缺 | 其中需先查證 | 其中伺服器不支援／不建議 | 其中可直接補 |")
    print("|---|---:|---:|---:|---:|---:|---:|")
    for n in sorted(base):
        c = dict.fromkeys(keys, 0)
        for r in base[n]:
            st = "部分" if actual and r.get("cat") == "K7" and r["state"] == "已有" else r["state"]
            c[st] += 1
            if st == "缺":
                c[r["kind"]] += 1
        for k in keys:
            total[k] += c[k]
        print(f"| {n}. {titles[n]} | {c['已有']} | {c['部分']} | {c['缺']} | {c['缺·需先查證']} | {c['缺·伺服器不支援或不建議']} | {c['缺·可直接補']} |")
    print(f"| **合計** | **{total['已有']}** | **{total['部分']}** | **{total['缺']}** | {total['缺·需先查證']} | {total['缺·伺服器不支援或不建議']} | {total['缺·可直接補']} |")


print("套用變更：" + "、".join(f"R{r - 9} {applied[r]} 列" for r in ROUNDS))
print("含預期：")
table(False)
print()
print("不算預期（K7 的列照實際狀態）：")
table(True)
print()
order = sorted({r["cat"] for n in base for r in base[n] if "cat" in r})
print("| 類 | " + " | ".join(f"§{n}" for n in sorted(base)) + " | 部分 | 缺 | 已有（預期） | 合計 |")
print("|---|" + "---:|" * (len(base) + 4))
for k in order:
    per = [sum(1 for r in base[n] if r.get("cat") == k) for n in sorted(base)]
    cnt = {s: sum(1 for n in base for r in base[n] if r.get("cat") == k and r["state"] == s) for s in STATES}
    print(f"| {k} | " + " | ".join(map(str, per)) + f" | {cnt['部分']} | {cnt['缺']} | {cnt['已有']} | {sum(cnt.values())} |")
if len(sys.argv) > 2 and sys.argv[2] == "--list":
    for n in sorted(base):
        for r in base[n]:
            if r["state"] != "已有" or "cat" in r:
                print(f"{n}\t{r['state']}\t{r.get('cat', '')}\t{r['key']}")
