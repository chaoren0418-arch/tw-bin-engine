"""T268 (#629): summarize delay/dir of SpellList.xml decrypted into scratch/. Usage: py -3 -I t268_spelllist.py FILE..."""
import sys, re, collections
sys.stdout.reconfigure(errors="replace")
for p in sys.argv[1:]:
    t = open(p, "rb").read().decode("utf-8", "replace")
    sp = re.findall(r"<(?:p?spell)\b([^>]*)>", t)
    d = collections.Counter(); dirs = collections.Counter(); ex = []
    for a in sp:
        at = dict(re.findall(r'(\w+)="([^"]*)"', a))
        d[at.get("delay")] += 1; dirs[at.get("dir", at.get("direction"))] += 1
        if at.get("id") in ("1","5","10","51","63") or len(ex) < 3: ex.append({k: at.get(k) for k in ("id","name","type","delay","dir","direction","target")})
    print(p, "spells", len(sp)); print(" delay", d.most_common(8)); print(" dir", dirs.most_common()); [print("  ", e) for e in ex[:9]]
