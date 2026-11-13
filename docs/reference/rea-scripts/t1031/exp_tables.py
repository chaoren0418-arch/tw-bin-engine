import sys, re, struct, hashlib, pathlib
# usage: exp_tables.py <pe file> <OriginalExpTable.cs>
import pefile
p = pathlib.Path(sys.argv[1]); data = p.read_bytes()
print('file', p.name, 'sha256', hashlib.sha256(data).hexdigest(), len(data))
th = [int(x) for x in re.findall(r'(\d+)UL', pathlib.Path(sys.argv[2]).read_text('utf-8'))]
try:
    pe = pefile.PE(data=data, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
    secs = [(s.Name.rstrip(b'\0').decode(errors='replace'), base + s.VirtualAddress, s.PointerToRawData, s.SizeOfRawData) for s in pe.sections]
except Exception as e:
    print('pe parse failed', e); secs = []; base = 0
def va(fo):
    for n, v, r, sz in secs:
        if r <= fo < r + sz: return v + fo - r, n
    return 0, '?'
def find_all(pat):
    out = []; i = data.find(pat)
    while i >= 0: out.append(i); i = data.find(pat, i + 1)
    return out
for label, seq in (('client-like 4^n', [1296, 2401, 4096, 6561, 10000, 14641]), ('server-like', [1296, 2401, 4096, 6581, 10000, 14661]), ('head', [125, 300, 500, 750])):
    for w, fmt in ((4, '<I'), (8, '<Q')):
        hits = find_all(b''.join(struct.pack(fmt, v) for v in seq))
        print(label, 'width', w, len(hits), [(hex(h), hex(va(h)[0]), va(h)[1]) for h in hits[:8]])
def dump(fo, w):
    fmt = '<Q' if w == 8 else '<I'
    # walk back to table start: previous entry should be 0 (level-1 base) -> find start as first of head 125
    vals = []; i = fo
    while True:
        v = struct.unpack_from(fmt, data, i)[0]
        if vals and v <= vals[-1]: break
        vals.append(v); i += w
    return vals
for w, fmt in ((4, '<I'), (8, '<Q')):
    for h in find_all(b''.join(struct.pack(fmt, v) for v in [125, 300, 500, 750])):
        vals = dump(h, w)
        same = sum(1 for i, v in enumerate(vals) if i + 1 < len(th) and th[i + 1] == v)
        firstdiff = next(((i + 1, th[i + 1] if i + 1 < len(th) else None, v) for i, v in enumerate(vals) if i + 1 >= len(th) or th[i + 1] != v), None)
        diffs = [(i + 1, th[i + 1] - v) for i, v in enumerate(vals) if i + 1 < len(th) and th[i + 1] != v]
        print(f'table FO {h:#x} VA {va(h)[0]:#x} {va(h)[1]} width {w}: {len(vals)} increasing entries, last {vals[-1]}, equal-to-product {same}, first diff (level,product,client) {firstdiff}')
        print('   diffs (level, product-client) first 50:', diffs[:50])
        print('   entries 45..', vals[44:60])
        print('   sha256 of entries (LE bytes)', hashlib.sha256(b"".join(struct.pack(fmt, v) for v in vals)).hexdigest())
