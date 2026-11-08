"""T225 (#532): list S250 (S_OPCODE_PACKETBOX) subcodes the 3.81 server source can emit. Read-only.
A subcode counts when (1) a packet class writes C(S_OPCODE_PACKETBOX) then C(<literal|constant>), or
(2) a packet class takes the subcode as a parameter and some caller passes Class.CONST / a literal.
Usage: py -3 -I t225_s250.py <server src dir> <out.json>"""
import re, sys, json, pathlib

root = pathlib.Path(sys.argv[1])
files = list(root.rglob("*.java"))
text = {f: f.read_text(encoding="utf-8", errors="replace") for f in files}
NUM = r"0x[0-9a-fA-F]+|\d+"
consts = {}
for f, t in text.items():
    consts[f.stem] = {n: int(v, 0) for n, v in re.findall(rf"static final int (\w+)\s*=\s*({NUM})\s*;", t)}
writers = {f.stem: t for f, t in text.items() if "S_OPCODE_PACKETBOX" in t and "serverpackets" in str(f)}
used = {}


def add(v, who):
    used.setdefault(v, set()).add(who)


def resolve(tok, cls):
    tok = tok.strip()
    if re.fullmatch(NUM, tok):
        return int(tok, 0)
    if "." in tok:
        c, n = tok.rsplit(".", 1)
        return consts.get(c.split(".")[-1], {}).get(n)
    return consts.get(cls, {}).get(tok, consts.get("S_PacketBox", {}).get(tok))


param = set()
for cls, t in writers.items():
    for tok in re.findall(r"writeC\(\s*S_OPCODE_PACKETBOX\s*\)\s*;[^\n]*\n(?:\s*//[^\n]*\n)*\s*(?:this\.)?writeC\(\s*([^;]+?)\s*\)\s*;", t):
        v = resolve(tok, cls)
        if v is None:
            param.add(cls)  # subcode comes from a parameter / expression
        else:
            add(v, f"{cls}:{tok.split('.')[-1]}")
# callers passing constants of parameterised writers
for f, t in text.items():
    for cls in param:
        for tok in re.findall(rf"new {cls}\(\s*([\w.]+)\s*[,)]", t):
            v = resolve(tok, cls)
            if v is not None and (tok.split(".")[0] in (cls, "S_PacketBox") or re.fullmatch(NUM, tok) or "." not in tok):
                if "." not in tok and not re.fullmatch(NUM, tok) and f.stem != cls:
                    continue
                add(v, f"{cls}:{tok.split('.')[-1]}")
res = {str(k): sorted(v) for k, v in sorted(used.items())}
pathlib.Path(sys.argv[2]).write_text(json.dumps({"param_writers": sorted(param), "subcodes": res}, ensure_ascii=False, indent=1), encoding="utf-8")
print("param writers:", ", ".join(sorted(param)))
for k, v in sorted(used.items()):
    print(k, hex(k), "; ".join(sorted(v))[:140])
