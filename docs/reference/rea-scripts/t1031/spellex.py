import sys, pathlib, re, collections
# usage: spellex.py <SpellExDesc-c.tbl> <spelldesc-c.tbl>
t = pathlib.Path(sys.argv[1]).read_bytes().decode('cp950', errors='replace').splitlines()
rows = {}
for l in t[1:]:
    m = re.match(r'^(\d+)\s+(.*)$', l)
    if not m: continue
    body = m.group(2)
    tg = re.search(r'對象：(?:\\f.)?\s*([^\^]*)', body)
    mp = re.search(r'魔力/體力\s*\((\d+)/(\d+)', body)
    rows[int(m.group(1)) + 1] = (tg.group(1).strip() if tg else None, mp.groups() if mp else None)
c = collections.Counter(v[0] for v in rows.values())
print('rows', len(rows), 'with 對象', sum(1 for v in rows.values() if v[0]), 'distinct 對象', len(c))
print('施法者 exact:', sorted(i for i, v in rows.items() if v[0] == '施法者'))
print('top', c.most_common(15))
for i in (1, 3, 4, 8): print(i, rows.get(i))
# mp cross-check between the two client tables
s = pathlib.Path(sys.argv[2]).read_bytes().decode('cp950', errors='replace').splitlines()
sd = {}
for l in s[1:]:
    m = re.match(r'^(\d+)\s+(.*?)\s*\((\d+)/(\d+)(?:/(\d+))?\)\s*$', l)
    if m: sd[int(m.group(1)) + 1] = (m.group(2), m.group(3), m.group(4), m.group(5))
print('spelldesc parsed with (mp/hp[/x])', len(sd), 'of', len([l for l in s[1:] if l.strip()]))
unparsed = [l for l in s[1:] if l.strip() and not re.match(r'^(\d+)\s+(.*?)\s*\((\d+)/(\d+)(?:/(\d+))?\)\s*$', l)]
print('unparsed lines', unparsed[:10])
mm = [(i, sd[i][1:3], rows[i][1]) for i in sd if i in rows and rows[i][1] and tuple(sd[i][1:3]) != tuple(rows[i][1])]
print('mp/hp mismatch spelldesc vs SpellExDesc', len(mm), mm[:15])
print('third field present', [(i, v[0], v[3]) for i, v in sd.items() if v[3]][:20])
