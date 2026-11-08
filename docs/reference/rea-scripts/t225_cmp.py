"""T225 (#532): compare TW16 S_EVENT (op 108) cases, 3.81 server S250 subcodes, engine S250 subcodes.
Usage: py -3 -I t225_cmp.py s_event_cases.txt s250_server.json"""
import json, sys, re

ENGINE = {5, 10, 11, 17, 18, 19, 20, 21, 22, 34, 35, 36, 40, 41, 42, 44, 57, 104, 105, 106, 110, 111, 154, 171}
tw = {}
for l in open(sys.argv[1], encoding="utf-8-sig", errors="replace"):
    lab, reads, rest = l.split("|")[0], l.split("|")[1].strip(), l
    for x in lab.split(","):
        x = x.strip()
        if x != "default":
            tw[int(x, 0)] = (reads, l.split("| calls")[1].split("| L")[0].strip(), re.search(r"\| str (.*?) \| calls", l).group(1).strip())
srv = {int(k): v for k, v in json.load(open(sys.argv[2], encoding="utf-8"))["subcodes"].items()}
print("TW16 cases", len(tw), "server subcodes", len(srv), "engine", len(ENGINE))
print("\n## server & TW16 & engine")
for k in sorted(set(srv) & set(tw) & ENGINE):
    print(k, hex(k), srv[k][0], "| tw", tw[k][0])
print("\n## server & TW16, engine missing")
for k in sorted((set(srv) & set(tw)) - ENGINE):
    print(k, hex(k), "; ".join(srv[k])[:70], "| tw reads", tw[k][0] or "-", "| str", tw[k][2][:30], "| calls", tw[k][1][:60])
print("\n## server only (no TW16 case)")
for k in sorted(set(srv) - set(tw)):
    print(k, hex(k), "; ".join(srv[k])[:90], "| engine" if k in ENGINE else "")
print("\n## engine but not server")
print(sorted(ENGINE - set(srv)))
print("\n## TW16 only (server never sends)")
print(" ".join(f"{k}" for k in sorted(set(tw) - set(srv))))
