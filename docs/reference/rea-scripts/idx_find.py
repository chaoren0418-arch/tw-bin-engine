"""Read-only: search every *.idx in the client dir for entry names containing the given substrings (case-insensitive).
idx layout as in ui_xml_decrypt.py: 4-byte header, then 28-byte records (off, name[20], size)."""
import os, struct, sys
sys.stdout.reconfigure(encoding="utf-8")
client = sys.argv[1]
keys = [k.lower() for k in sys.argv[2:]]
for fn in sorted(os.listdir(client)):
    if not fn.lower().endswith(".idx"):
        continue
    b = open(os.path.join(client, fn), "rb").read()
    hits = []
    for i in range(4, len(b) - 27, 28):
        name = b[i + 4:i + 24].split(b"\0")[0].decode("latin1", "replace").lower()
        if any(k in name for k in keys):
            hits.append(name)
    print(fn, len(b), hits[:40])
