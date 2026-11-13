import sys, pathlib, re
# usage: content_scan.py <extract root> <pak-index dir>
root = pathlib.Path(sys.argv[1]); idx = pathlib.Path(sys.argv[2])
files = [p for p in root.rglob('*') if p.is_file()]
print('files scanned', len(files))
def texts(p):
    b = p.read_bytes()
    out = [b.decode('cp950', errors='replace')]
    try: out.append(b.decode('utf-8'))
    except Exception: pass
    return out
groups = {
 'EXP thresholds (>=2 of 2401/6561/6581/14641/14661/20736/20756/28561/28581/1185921/1185937)': lambda t: sum(1 for k in ('2401','6561','6581','14641','14661','20736','20756','28561','28581','1185921','1185937') if re.search(r'(?<!\d)'+k+r'(?!\d)', t)) >= 2,
 'skill names (初級治癒術 & 光箭)': lambda t: '初級治癒術' in t and '光箭' in t,
 'server template ids (>=2 of 40010/40012/40013/40056/40079/40308/40743)': lambda t: sum(1 for k in ('40010','40012','40013','40056','40079','40308','40743') if re.search(r'(?<!\d)'+k+r'(?!\d)', t)) >= 2,
 'exp keyword (\\bexp\\b / 經驗值 with digits table)': lambda t: bool(re.search(r'(?i)\bexp(erience)?\b', t)),
 'ground gfx pairing (icon 80 & gfx 45 on one line / grdgfx)': lambda t: bool(re.search(r'(?i)grd|ground', t)),
}
for g, f in groups.items():
    hit = sorted({str(p.relative_to(root)) for p in files if any(f(t) for t in texts(p))})
    print(f'\n[{g}] {len(hit)} files'); print('  ' + ', '.join(hit[:60]))
# filename keyword scan across every list
kw = ['skill', 'spell', 'magic', 'exp', 'level', 'lvup', 'lvl', 'item', 'drop', 'ground', 'templ', 'etc', 'rule', 'catalog']
names = []
for l in sorted(idx.glob('*.list.txt')):
    for line in l.read_text('utf-8-sig', errors='replace').splitlines():
        m = re.match(r'^\d+\s+(\S+)', line.strip())
        if m: names.append((l.name.replace('.idx.list.txt', ''), m.group(1)))
print('\nall names', len(names), 'in', len({a for a, _ in names}), 'idx')
for k in kw:
    hit = sorted({f'{a}:{n}' for a, n in names if k in n.lower() and not re.search(r'\.(html?|h|ht|spr|til)$', n.lower())})
    htm = sum(1 for a, n in names if k in n.lower() and re.search(r'\.(html?|h|ht)$', n.lower()))
    print(f'[{k}] non-html {len(hit)} (html {htm}):', ', '.join(hit[:50]))
