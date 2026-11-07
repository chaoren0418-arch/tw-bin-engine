"""Scan .text for uses of object state flags at +0x28 (0x20 poison-green, 0x200 gray, 0x40/0x100 invisible). Linear sweep with skipdata."""
import sys, pathlib, re, collections
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
im = Img()
v, t = im.text()
md = Cs(CS_ARCH_X86, CS_MODE_32); md.skipdata = True
hits = collections.defaultdict(list)
load = re.compile(r"mov (e\w\w), dword ptr \[e\w\w \+ 0x28\]$")
direct = re.compile(r"(test|and) dword ptr \[e\w\w \+ 0x28\], 0x(\w+)$")
prev = []
for a, sz, m, o in md.disasm_lite(t, v):
    s = f"{m} {o}"
    d = direct.match(s)
    if d: hits[int(d.group(2), 16)].append(a)
    for (pa, reg) in prev:
        mm = re.match(rf"(and|test) {reg}, 0x(\w+)$", s)
        if mm: hits[int(mm.group(2), 16)].append(a)
    l = load.match(s)
    prev = ([(a, l.group(1))] if l else []) + prev[:1]
for f in (0x20, 0x200, 0x40, 0x100, 0x220):
    print(hex(f), len(hits[f]), " ".join(hex(x) for x in hits[f][:120]))
