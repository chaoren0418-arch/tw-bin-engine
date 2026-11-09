"""Read-only (T266 / #627): list.spr (TW13081901.txt) census for field 109 kinds 6 (EarthRising) and 7 (Eruption):
for each gfx print name, param, whether its frames carry '!' (hit) or ']' (sub effect); for kind 7 also the param gfx's
kind and '!' presence (the hit of an Eruption comes only from the kind-6 object built from the param at the end).
Usage: py -3 -I t266_kind67.py <TW13081901.txt>   (prints only; writes nothing)"""
import pathlib
import re
import sys

text = pathlib.Path(sys.argv[1]).read_bytes().decode("cp950", "replace")
spr = {}
cur = None
for line in text.splitlines():
    m = re.match(r"#(\d+)\s+\d+\s+(.*)", line)
    if m:
        cur = int(m.group(1))
        spr[cur] = {"name": m.group(2).strip()[:30], "kind": None, "param": None, "hit": 0, "sub": 0, "frames": 0}
        continue
    if cur is None:
        continue
    e = re.search(r"109\.effect\((-?\d+)\s+(-?\d+)\)", line)
    if e:
        spr[cur]["kind"], spr[cur]["param"] = int(e.group(1)), int(e.group(2))
        continue
    a = re.match(r"\s*\d+\.\w[^()]*\((.*)\)", line)
    if a and not re.match(r"\s*1\d\d\.", line):
        body = a.group(1)
        spr[cur]["hit"] += body.count("!")
        spr[cur]["sub"] += len(re.findall(r"\](-?\d+)", body))
        spr[cur]["frames"] += len(re.findall(r"\d+\.\d+:\d+", body))

for k in (6, 7):
    rows = [(g, s) for g, s in sorted(spr.items()) if s["kind"] == k]
    print(f"== kind {k}: {len(rows)} gfx")
    for g, s in rows:
        extra = ""
        if k == 7:
            p = spr.get(s["param"])
            extra = (f" | param {s['param']} {p['name']!r} kind={p['kind']} hit!={p['hit']} sub]={p['sub']}"
                     if p else f" | param {s['param']} (not in list.spr)")
        print(f"  {g:6d} {ascii(s['name']):32s} param={s['param']:6d} frames={s['frames']:3d} hit!={s['hit']} sub]={s['sub']}{extra}")
