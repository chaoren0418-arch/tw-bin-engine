"""T318 (#752): read-only analysis over PakViewer listings.

Inputs (all produced by the sibling scripts, never the client files themselves):
  scratch/pak-index/*.list.txt          pak_index.ps1  (PakViewer `pak list` of every .idx in 0-客戶端)
  scratch/pak-index/alt/*.list.txt      pak_index.ps1 -WithAlt (reference clients kor25 / t89 / ly)
  scratch/pak-extract/<src>/*           pak_extract.ps1 (PakViewer `pak extract` + `xml decrypt`)

Output: a plain-text report on stdout (redirect into scratch/). Usage:
  py -3 -I docs/reference/rea-scripts/pak/pak_find.py > scratch/pak-index/FIND.txt
"""
import os, re, sys

sys.stdout.reconfigure(encoding="utf-8")
IDX = os.path.join("scratch", "pak-index")
EXT = os.path.join("scratch", "pak-extract")


def load(path):
    out = []
    for line in open(path, encoding="utf-8-sig"):
        m = re.match(r"^(\d+)\s+(.+?)\s+([\d,]+)\s+0x", line)
        if m:
            out.append((m.group(2), int(m.group(3).replace(",", ""))))
    return out


main = {f[:-len(".list.txt")]: load(os.path.join(IDX, f)) for f in sorted(os.listdir(IDX)) if f.endswith(".list.txt")}
alt_dir = os.path.join(IDX, "alt")
alt = {f[:-len(".list.txt")]: load(os.path.join(alt_dir, f)) for f in sorted(os.listdir(alt_dir)) if f.endswith(".list.txt")} if os.path.isdir(alt_dir) else {}
where = {}
for pak, rows in main.items():
    for n, s in rows:
        where.setdefault(n.lower(), []).append((pak, s))

print("== searched paks (0-客戶端) ==")
for pak, rows in main.items():
    print(f"{pak}\t{len(rows)} files")
print(f"total names: {sum(len(r) for r in main.values())}")

# --- 1. exact-name targets -------------------------------------------------
TARGETS = [
    "LoginUI.xml", "SelectUI.xml", "CreateUI.xml", "ServerName.xml", "LoadingScene.xml", "LoadingWindow.xml",
    "RenewalMailUI.xml", "ResetStatUI.xml", "StatInfoUI.xml", "ReNStatusUIEx.xml", "AgitAuctionUI.xml",
    "FishingUI.xml", "FishObtainUI.xml", "OptionUI.xml", "GameRankingUI.xml", "SpellList.xml", "summonList.xml",
    "recipe.tbl", "monlist-c.html", "summonlist-c.html", "string-c.tbl", "9183.img", "9184.img",
]
print("\n== exact targets (also -c / -h variants), 0-客戶端 vs reference clients ==")
for t in TARGETS:
    base, ext = os.path.splitext(t)
    cands = [t, f"{base}-c{ext}", f"{base}-h{ext}"]
    hits = [(c, where[c.lower()]) for c in cands if c.lower() in where]
    althits = []
    for k, rows in alt.items():
        names = {n.lower(): s for n, s in rows}
        for c in cands:
            if c.lower() in names:
                althits.append(f"{k}:{c}({names[c.lower()]})")
    print(f"{t}\tmain={hits if hits else 'ABSENT'}\talt={' '.join(sorted(set(althits))) or '-'}")

# --- 2. keyword sweep (catches renamed / truncated names) -------------------
KEYS = ["login", "selectui", "createui", "servername", "loading", "mail", "resetstat", "statinfo", "renstatus",
        "agitauction", "fishing", "optionui", "ranking", "spelllist", "font", "9183", "9184"]
print("\n== keyword sweep over every 0-客戶端 pak ==")
for k in KEYS:
    hits = sorted({f"{pak}:{n}" for pak, rows in main.items() for n, _ in rows if k in n.lower()})
    print(f"{k}\t{len(hits)}\t{' '.join(hits[:12])}{' …' if len(hits) > 12 else ''}")

# --- 3. surf coverage of reference XMLs against all 0-客戶端 paks -------------
print("\n== surf ids referenced by reference-client XML, looked up in all 0-客戶端 paks ==")
src = os.path.join(EXT, "kor25")
for fn in sorted(os.listdir(src)) if os.path.isdir(src) else []:
    if not fn.endswith(".dec.xml"):
        continue
    t = open(os.path.join(src, fn), "rb").read().decode("utf-8", "replace")
    ids = sorted({int(m) for m in re.findall(r'(?:Surf|surfNumber|SurfNum)\w*\s*=\s*"(\d+)', t, re.I)})
    img, lt, tbt, png, none = [], [], [], [], []
    for i in ids:
        w = where.get(f"{i}.img", [])
        if any(not p.startswith("LiTo_") for p, _ in w):
            img.append(i)
        elif w:
            lt.append(i)
        elif f"{i}.tbt" in where:
            tbt.append(i)
        elif f"{i}.png" in where:
            png.append(i)
        else:
            none.append(i)
    name = fn.split("__", 1)[1].replace(".dec.xml", "")
    print(f"{name}\tsurfs={len(ids)}\timg={len(img)}\tLTsurf_only={len(lt)}\ttbt_only={len(tbt)}\tpng_only={len(png)}"
          f"\tabsent={len(none)}\tabsent_ids={none}")
    if none:
        for client in ("kor25", "t89", "ly"):
            names = {n.lower() for k, rows in alt.items() if k.startswith(client + ".Sprite") for n, _ in rows}
            if names:
                got = [i for i in none if f"{i}.img" in names or f"{i}.tbt" in names or f"{i}.png" in names]
                print(f"    {client} Sprite*.idx has {len(got)}/{len(none)} of the absent ids")

# --- 4. string table entry 4118 ---------------------------------------------
print("\n== string-c.tbl entry 4118 (line 0 = count header, ids zero-based) ==")
for sub in ("main", "kor25", "ly"):
    d = os.path.join(EXT, sub)
    for fn in sorted(os.listdir(d)) if os.path.isdir(d) else []:
        if not fn.endswith("string-c.tbl"):
            continue
        lines = open(os.path.join(d, fn), "rb").read().split(b"\n")
        hdr = lines[0].strip().decode("ascii", "replace")
        val = lines[4119].rstrip(b"\r").decode("cp950", "replace") if len(lines) > 4119 else "(absent)"
        print(f"{sub}/{fn}\theader={hdr}\tlines={len(lines) - 1}\t4118={val}")
