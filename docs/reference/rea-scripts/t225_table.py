"""T225 (#532): render the S250 comparison as markdown rows (scratch only).
Usage: py -3 -I t225_table.py s_event_cases.txt s250_server.json"""
import json, sys, re
sys.stdout.reconfigure(encoding="utf-8")
ENGINE = {5: "Duel.cs", 10: "BasicSupplies.cs", 11: "BasicSupplies.cs", 17: "Buddies.cs", 18: "Buddies.cs", 19: "Buddies.cs",
          20: "MagicStatus.cs", 21: "MagicStatus.cs", 22: "MagicStatus.cs", 34: "MagicStatus.cs", 35: "MagicStatus.cs",
          36: "MagicStatus.cs", 40: "MagicStatus.cs", 41: "CharacterConfig.cs", 42: "CharacterManagement.cs", 44: "MagicStatus.cs",
          57: "MagicStatus.cs", 104: "Party.cs", 105: "Party.cs", 106: "Party.cs", 110: "Party.cs", 111: "LocationShare.cs",
          154: "MagicStatus.cs", 171: "Clan.cs"}
tw = {}
for l in open(sys.argv[1], encoding="utf-8-sig"):
    lab = l.split("|")[0]
    for x in lab.split(","):
        x = x.strip()
        if x != "default":
            reads = l.split("|")[1].strip()
            strs = re.search(r"\| str (.*?) \| calls", l).group(1).strip()
            line = int(re.search(r"\| L (\d+)", l).group(1))
            tw[int(x, 0)] = (reads, strs, line)
srv = {int(k): v for k, v in json.load(open(sys.argv[2], encoding="utf-8"))["subcodes"].items()}
for k in sorted(set(srv) & set(tw)):
    names = sorted({v.split(":")[1] + "（" + v.split(":")[0] + "）" for v in srv[k] if not v.startswith("S_Classic") and not v.startswith("ServerBase")})
    r, s, _ = tw[k]
    ids = " ".join(x for x in s.split() if x.isdigit())
    eng = ENGINE.get(k, "—")
    print(f"| {k}／0x{k:02X} | {'、'.join(names)} | `{r or '（另函式讀）'}` | {ids or '—'} | {('`'+eng+'`') if k in ENGINE else '缺'} |")
