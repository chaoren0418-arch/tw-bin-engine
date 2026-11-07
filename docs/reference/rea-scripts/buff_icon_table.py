"""V3 (#458): build "effect -> icon" rows from the client's own effectlist2.xml + <icon>.tbt headers. Read-only.

Inputs : a directory of decrypted Tile.pak XMLs (from ui_xml_decrypt.py, under scratch/) and the client root
         (Sprite*.idx/pak are only read to locate <icon>.tbt and hash it). Nothing from the client is written.
Output : markdown table on stdout (only numbers and effect names; no pixels).
Usage  : py -3 -I docs/reference/rea-scripts/buff_icon_table.py scratch/rea/uixml "D:\\lineage開發\\0-客戶端" [-o scratch/out.md] [effect ids...]
"""
import glob, os, re, struct, sys
import xml.etree.ElementTree as ET

xmldir, root = sys.argv[1], sys.argv[2]
args = sys.argv[3:]
out = open(args.pop(args.index("-o") + 1), "w", encoding="utf-8") if "-o" in args else sys.stdout
if "-o" in args: args.remove("-o")
want = [int(a) for a in args]


def load(name):
    raw = open(os.path.join(xmldir, name), "rb").read()
    # Attribute values are ASCII here; CP950 bytes in comments must not break the parser.
    return ET.fromstring(re.sub(rb"<!--.*?-->", b"", raw, flags=re.S).decode("cp950", "replace"))


effects = {int(e.get("id")): e.attrib for e in load("effectlist2.xml").iter("effect")}

# The icon catalog (sub_88CE60 -> sub_87B3A0) loads "<icon>.tbt" by number (format "%d.tbt", VA 0x117D924).
# It does not go through SurfList.xml. tbt header bytes 2/3 read as width/height (32x32 for every buff icon seen).
import hashlib
tbts = {}
for idx in sorted(glob.glob(os.path.join(root, "Sprite*.idx"))):
    b = open(idx, "rb").read()
    with open(idx[:-4] + ".pak", "rb") as p:
        for i in range(4, len(b) - 27, 28):
            off, = struct.unpack_from("<I", b, i)
            nm = b[i + 4:i + 24].split(b"\0")[0].decode("latin1").lower()
            sz, = struct.unpack_from("<I", b, i + 24)
            if nm.endswith(".tbt") and nm[:-4].isdigit():
                p.seek(off); data = p.read(sz)
                tbts.setdefault(int(nm[:-4]), []).append((os.path.basename(idx)[:-4], data[2], data[3], hashlib.sha256(data).hexdigest()))


def surf(s):
    if s is None or int(s) <= 0:
        return "—"
    s = int(s)
    hits = tbts.get(s)
    if not hits:
        return f"{s}（缺 .tbt）"
    dims = sorted({f"{w}×{h}" for _, w, h, _ in hits})
    where = "+".join(sorted({n.replace("Sprite", "S") or "S" for n, _, _, _ in hits}))
    diff = "，內容不同" if len({hsh for *_, hsh in hits}) > 1 else ""
    return f"{s} {'／'.join(dims)}（{where}{diff}）"

print("| effect | 名稱（startMsgName） | kind | group | priority | duration | icon（.tbt） | offIcon（.tbt） | gauge |", file=out)
print("|---|---|---|---|---|---|---|---|---|", file=out)
for i in want or sorted(effects):
    e = effects.get(i)
    if e is None:
        print(f"| {i} | （effectlist2 無此 id） |  |  |  |  |  |  |  |", file=out)
        continue
    g = e.get("gaugePng")
    print(f"| {i} | {e.get('startMsgName', '')} | {e.get('kind', '—')} | {e.get('group', '')} | {e.get('priority', '—')} | "
          f"{e.get('duration', '')} | {surf(e.get('icon'))} | {surf(e.get('offIcon'))} | {g + '/' + e.get('gaugeType', '') if g else '—'} |", file=out)

# Icons present in both Sprite.pak and a SpriteNN.pak with different bytes: how many distinct Sprite.pak copies?
used = {int(e.get(k)) for i, e in effects.items() if (not want or i in want) for k in ("icon", "offIcon") if e.get(k) and int(e.get(k)) > 0}
dup = [s for s in sorted(used) if len({h for *_, h in tbts.get(s, [])}) > 1]
base = {h for s in dup for n, _, _, h in tbts[s] if n == "Sprite"}
print(f"\n同名不同內容：{len(dup)} 個圖示；其 Sprite.pak 版本共 {len(base)} 種內容（SHA-256 前 12 碼：{', '.join(sorted(h[:12] for h in base)[:5])}）", file=out)
