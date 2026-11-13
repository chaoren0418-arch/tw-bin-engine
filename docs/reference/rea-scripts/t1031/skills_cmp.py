import sys, json, re, pathlib, collections, hashlib
# usage: skills_cmp.py <baseline skills.json> <spelldesc tbl> [more tbl...]
base = pathlib.Path(sys.argv[1])
raw = base.read_bytes()
print('baseline sha256', hashlib.sha256(raw).hexdigest(), 'bytes', len(raw))
rows = json.loads(raw.decode('utf-8-sig'))
print('rows', len(rows), 'keys', sorted(rows[0].keys()))
ids = [r['Id'] for r in rows]
print('id min/max', min(ids), max(ids), 'distinct', len(set(ids)))
print('id<=220:', sum(1 for i in ids if i <= 220), ' 221..256:', sum(1 for i in ids if 220 < i <= 256), ' >256:', sum(1 for i in ids if i > 256))
lv = collections.Counter(r['Level'] for r in rows)
print('levels', sorted(lv.items()))
bymap = {r['Id']: r for r in rows}
# level formula check
bad = [(r['Id'], r['Level']) for r in rows if r['Id'] <= 220 and r['Level'] != (r['Id'] - 1) // 8 + 1]
print('id<=220 level != (id-1)//8+1 :', len(bad), bad[:40])
hi = [(r['Id'], r['Level'], r['Name']) for r in rows if r['Id'] > 220]
print('id>220 sample', hi[:12], '...', hi[-5:])
print('id>220 level counter', sorted(collections.Counter(l for _, l, _ in hi).items()))
missing_low = [i for i in range(1, 221) if i not in bymap]
print('ids 1..220 absent in baseline', missing_low)
for t in sys.argv[2:]:
    p = pathlib.Path(t)
    lines = p.read_bytes().decode('cp950', errors='replace').splitlines()
    tbl = {}
    for l in lines[1:]:
        m = re.match(r'^(\d+)\s+(.*?)\s*\(([^)]*)\)\s*$', l)
        if m:
            tbl[int(m.group(1)) + 1] = (m.group(2), m.group(3))
        elif l.strip():
            m2 = re.match(r'^(\d+)\s+(.*)$', l)
            if m2: tbl[int(m2.group(1)) + 1] = (m2.group(2).strip(), None)
    print(f'\n== {p.name}: header {lines[0]!r} parsed {len(tbl)} ids {min(tbl)}..{max(tbl)}')
    same = diff = 0; diffs = []; only_tbl = []; only_base = []
    for i in range(1, 257):
        b = bymap.get(i); c = tbl.get(i)
        if b and c:
            if b['Name'] == c[0]: same += 1
            else: diff += 1; diffs.append((i, b['Name'], c[0]))
        elif c: only_tbl.append((i, c[0]))
        elif b: only_base.append((i, b['Name']))
    print('name same', same, 'diff', diff, 'only tbl', len(only_tbl), 'only baseline', len(only_base))
    for d in diffs: print('  DIFF', d)
    print('  only tbl', only_tbl)
    print('  only baseline', only_base)
