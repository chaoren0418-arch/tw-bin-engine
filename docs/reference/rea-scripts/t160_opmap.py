"""T160 (#428): map TW16 receive-handler call sites to opcode + packet name. Read-only.

First dispatch group of sub_947530 (index 0x94828C, targets 0x9480C0, #417) gives the case block of each
opcode; the name comes from the packet-name table used by opnames.py (sub_9515E0).
Usage: py -3 -I docs/reference/rea-scripts/t160_opmap.py
"""
import struct, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img

im = Img()
SITES = {  # call site in the dispatcher -> handler (found with IDA xrefs, see actions-and-names.md)
    0x94786A: "sub_946CF0 polymorph (dhchc)",
    0x9478AE: "sub_94DB20 title (ds)",
    0x94779E: "sub_94FE30 weapon stance (dcc)",
    0x947CD2: "sub_933640 (ddhhcs) -> title/lawful/gfx",
    0x947A8F: "sub_942030 (ds) -> +248 owner, +240",
    0x947C06: "sub_946770 (dssd) -> clan name/title/clan id",
    0x947B21: "sub_932510 (ds) -> name",
    0x947B32: "sub_932350 (dh) -> lawful",
}


def cstr(va):
    return im.read(va, 64).split(b"\0")[0].decode("latin1")


names = {}
for op in range(255):
    idx = im.read(0x951E78 + op, 1)[0]
    tgt = im.d32(0x951C5C + 4 * idx)
    ins = im.read(tgt, 7)
    if ins[:3] == b"\xc7\x45\xfc":
        names[op] = cstr(struct.unpack("<I", ins[3:7])[0])

cases = {}
for op in range(255):
    idx = im.read(0x94828C + op, 1)[0]
    if idx > 114:
        continue
    cases.setdefault(im.d32(0x9480C0 + 4 * idx), []).append(op)
starts = sorted(cases)
for site, label in SITES.items():
    blk = max((s for s in starts if s <= site), default=None)
    ops = cases.get(blk, [])
    print(hex(site), label, "case", hex(blk) if blk else None, [(o, names.get(o)) for o in ops])
