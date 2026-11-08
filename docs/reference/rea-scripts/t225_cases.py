"""T225 (#532): split the top-level `switch` of a Hex-Rays pseudocode file into cases and summarize
each case: field readers (C/H/D/Q/S), string-table ids, called functions. Read-only.
Usage: py -3 -I t225_cases.py FILE.c [switch_line_substring]"""
import re, sys, json, pathlib

READ = {"sub_926C70": "c", "sub_926B10": "c", "sub_926BF0": "h", "sub_926CA0": "h",
        "sub_926B80": "d", "sub_926B40": "q", "sub_926C40": "s"}
src = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8").splitlines()
key = sys.argv[2] if len(sys.argv) > 2 else "switch ("
start = next(i for i, l in enumerate(src) if key in l)
depth, i = 0, start + 1
cases, cur = [], None
base = None
while i < len(src):
    l = src[i]
    if base is None and l.strip() == "{":
        base = depth + 1
    depth += l.count("{") - l.count("}")
    if base is not None and depth < base:
        break
    m = re.match(r"\s*(case [^:]+|default):\s*$", l)
    if m and depth == base:
        if cur is not None and not cur["body"]:
            cur["labels"].append(m.group(1))
        else:
            cur = {"labels": [m.group(1)], "body": [], "line": i + 1}
            cases.append(cur)
    elif cur is not None:
        cur["body"].append(l)
    i += 1
out = []
for c in cases:
    b = "\n".join(c["body"])
    rd = "".join(READ[x] for x in re.findall(r"sub_926[0-9A-F]{3}", b) if x in READ)
    strs = re.findall(r"sub_4EAFF0\(([^)]*)\)", b)
    calls = [x for x in dict.fromkeys(re.findall(r"sub_[0-9A-F]{6,7}", b)) if x not in READ and x != "sub_4EAFF0"]
    labs = [x.replace("case ", "").rstrip("u") for x in c["labels"]]
    out.append({"cases": labs, "line": c["line"], "reads": rd, "str": strs[:8], "calls": calls[:10], "n": len(c["body"])})
for o in out:
    print(",".join(o["cases"]), "|", o["reads"], "| str", " ".join(o["str"]), "| calls", " ".join(o["calls"]), "| L", o["line"], "n", o["n"])
