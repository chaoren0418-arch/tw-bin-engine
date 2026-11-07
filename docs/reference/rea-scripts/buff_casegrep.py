"""V3 (#458). Usage: py -3 -I buff_casegrep.py scratch/rea/ida/q1/009357E0.c
In a Hex-Rays dump, print each icon call (sub_F05A90/F059F0/F05B10/8FD8E0) with the innermost enclosing case labels."""
import re, sys
lines = open(sys.argv[1], encoding="utf-8").read().splitlines()
pat = re.compile(sys.argv[2] if len(sys.argv) > 2 else r"sub_F05A90|sub_F059F0|sub_F05B10")
stack = []  # (indent, label)
for n, l in enumerate(lines, 1):
    m = re.match(r"(\s*)(case [^:]+:|default:|switch \(.*|if \(.*|else if \(.*)", l)
    if m:
        ind = len(m.group(1))
        while stack and stack[-1][0] >= ind:
            stack.pop()
        stack.append((ind, m.group(2).strip()[:70]))
    if pat.search(l):
        ind = len(l) - len(l.lstrip())
        ctx = [s for i, s in stack if i < ind]
        print(n, " | ".join(ctx[-4:]), "=>", l.strip()[:110])
