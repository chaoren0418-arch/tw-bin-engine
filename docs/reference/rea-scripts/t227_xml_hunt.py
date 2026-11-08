"""T227 (#534) read-only: hunt login-flow UI XML in every local client copy.
Checks (1) loose files by name, (2) entry names in every *.idx (4-byte header + 28-byte records: off, name[20], size),
(3) loose .pak-less archives are not opened. Usage: py -3 -I xml_hunt.py <root> <out.json>"""
import json, os, sys

root, out = sys.argv[1], sys.argv[2]
keys = ["loginui", "selectui", "createui", "loadingscene", "servername", "loadingwindow", "noticewindow", "quitwindow"]
skip = {".git", "scratch", "node_modules", "obj", "bin"}
loose, idx_hits, idx_seen = [], {}, []
for d, dirs, files in os.walk(root):
    dirs[:] = [x for x in dirs if x not in skip]
    for fn in files:
        low = fn.lower()
        p = os.path.join(d, fn)
        if any(k in low for k in keys):
            loose.append(p)
        if low.endswith(".idx"):
            try:
                b = open(p, "rb").read()
            except OSError:
                continue
            idx_seen.append(p)
            hits = []
            for i in range(4, len(b) - 27, 28):
                name = b[i + 4:i + 24].split(b"\0")[0].decode("latin1", "replace").lower()
                if any(k in name for k in keys):
                    hits.append(name)
            if hits:
                idx_hits[p] = sorted(set(hits))
res = dict(root=root, idx_files=len(idx_seen), idx_dirs=sorted({os.path.dirname(p) for p in idx_seen}), idx_hits=idx_hits, loose=loose)
open(out, "w", encoding="utf-8").write(json.dumps(res, ensure_ascii=False, indent=1))
sys.stdout.reconfigure(encoding="utf-8")
print(json.dumps(dict(idx_files=len(idx_seen), dirs=len(res["idx_dirs"]), idx_hits=idx_hits, loose=loose[:40]), ensure_ascii=False, indent=1))
