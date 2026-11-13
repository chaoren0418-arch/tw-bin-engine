import sys, pathlib
# usage: peek.py <file> [maxlines] [encoding]
p = pathlib.Path(sys.argv[1]); n = int(sys.argv[2]) if len(sys.argv) > 2 else 40
enc = sys.argv[3] if len(sys.argv) > 3 else 'cp950'
b = p.read_bytes()
t = b.decode(enc, errors='replace')
lines = t.splitlines()
print(f'## {p.name} bytes={len(b)} lines={len(lines)}')
for i, l in enumerate(lines[:n]):
    print(i, repr(l[:160]))
