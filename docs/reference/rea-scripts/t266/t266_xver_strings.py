"""Read-only (T266 / #627): look for TRADE_WINDOW / loading / projectile-related ASCII strings in two binaries
(TW16 and the LiTo 3.81 OEP snapshot) and print SHA-256, hit offsets and a short context. Writes nothing.
Usage: py -3 -I t266_xver_strings.py <TW1608262200.bin> <LiTo.unpacked-oep-current.exe>"""
import hashlib
import re
import sys

WORDS = [b"TRADE_WINDOW", b"TradeWindow", b"TradeBox", b"TradeItem", b"CNpcShop", b"Items_List", b"uiextra86", b"uiextra",
         b"LoadingWindowUI", b"LoadingScene", b"Loading", b"touma3", b"gayle5", b"GerengEv3", b"HorunEv3", b"Siriss1",
         b"Eruption", b"TRANSACTION_WINDOW", b"HYPER_TEXT_WINDOW", b"QUIT_WINDOW"]

for path in sys.argv[1:3]:
    data = open(path, "rb").read()
    print(f"== {path}\n   size={len(data)} sha256={hashlib.sha256(data).hexdigest()}")
    for w in WORDS:
        hits = [m.start() for m in re.finditer(re.escape(w), data)]
        ctx = ""
        if hits:
            o = hits[0]
            ctx = data[max(0, o - 12):o + len(w) + 12].decode("latin1").replace("\0", ".")
        print(f"   {w.decode():20s} hits={len(hits):3d} first={hex(hits[0]) if hits else '-':10s} {ascii(ctx)}")
