import sys, re, struct, pathlib, hashlib
# usage: qdump.py <file> <fo hex of a known entry> <OriginalExpTable.cs>  -> qword table around, compared to product thresholds
data = pathlib.Path(sys.argv[1]).read_bytes(); fo = int(sys.argv[2], 16)
th = [int(x) for x in re.findall(r'(\d+)UL', pathlib.Path(sys.argv[3]).read_text('utf-8'))]
q = lambda o: struct.unpack_from('<Q', data, o)[0]
s = fo
while s >= 8 and q(s - 8) < q(s) and q(s - 8) != 0: s -= 8
if q(s - 8) == 0: s -= 8
vals = [q(s)]; o = s + 8
while q(o) > vals[-1]: vals.append(q(o)); o += 8
print(f'table start FO {s:#x}, {len(vals)} entries (index0={vals[0]}), end FO {o:#x}, next qword {q(o)}')
print('entries', vals)
# align: find index of 1296
k = vals.index(1296)
print('1296 at index', k, '(product th index 5)')
off = 5 - k
d = [(i + off, th[i + off], v, th[i + off] - v) for i, v in enumerate(vals) if 0 <= i + off < len(th) and th[i + off] != v]
print('aligned offset', off, 'diff count', len(d), 'of', len(vals))
print('diffs (productIndex, product, client, delta):', d)
print('sha256 LE', hashlib.sha256(b''.join(struct.pack('<Q', v) for v in vals)).hexdigest())
