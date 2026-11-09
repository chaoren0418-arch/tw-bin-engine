"""T268 (#629): map HyperAct names to constructors from the sub_BB5180 pseudocode. Usage: py -3 -I t268_hyper.py BB5180.c out.json"""
import re, sys, json
t = open(sys.argv[1], encoding="utf-8").read().splitlines()
pairs = []
last = None
for line in t:
    m = re.search(r"= (sub_[0-9A-F]+)\(v\d+\);", line)
    if m: last = m.group(1)
    m2 = re.search(r'sub_[0-9A-F]+\(v\d+, "([^"]+)", v\d+\)', line)
    if m2:
        pairs.append((m2.group(1), last)); last = None
json.dump(pairs, open(sys.argv[2], "w"), indent=0)
print(len(pairs))
