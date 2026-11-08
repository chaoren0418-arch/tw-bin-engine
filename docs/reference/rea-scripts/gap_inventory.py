"""T202 (#504): one-shot static inventory of TW1608262200.bin features for gap-matrix.md. Read-only.

Emits JSON (argv[1], default scratch/t202/inventory.json):
  recv1  : opcode -> name (sub_9515E0 table), case block VA/FO in sub_947530 group 1, direct calls in block
  recv2  : group 2 (state != 3, opcode-6), same fields
  recv3  : group 3 (sub-id - 0x13B), same fields
  send   : C_* names (sub_951F80 table, see wo_copnames.py)
  xml    : UI XML file names found in .rdata/.data with code references (function start VA/FO)
  rtti   : UI-ish RTTI class names (UI / Window / Wnd / Dlg)
Usage: py -3 -I docs/reference/rea-scripts/gap_inventory.py [out.json]
"""
import json, re, struct, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img

im = Img()
END = 0x9480AE  # common epilogue of sub_947530
DEFAULT1 = 0x947DF4


def h(v):
    return None if v is None else f"0x{v:X}"


def fo(v):
    o = im.off(v)
    return None if o is None else f"0x{o:X}"


def cstr(va, n=64):
    b = im.read(va, n)
    return b.split(b"\0")[0].decode("latin1") if b else ""


names = {}
for op in range(255):
    idx = im.read(0x951E78 + op, 1)[0]
    tgt = im.d32(0x951C5C + 4 * idx)
    ins = im.read(tgt, 7)
    if ins[:3] == b"\xc7\x45\xfc":
        names[op] = cstr(struct.unpack("<I", ins[3:7])[0])


def group(index, target, n, bias, maxidx):
    idx = im.read(index, n)
    blocks = {}
    for i, b in enumerate(idx):
        if b > maxidx:
            continue
        blocks.setdefault(im.d32(target + 4 * b), []).append(i + bias)
    starts = sorted(blocks)
    out = []
    for k, s in enumerate(starts):
        stop = starts[k + 1] if k + 1 < len(starts) else s + 0x200
        calls = []
        for ins in im.dis(s, min(stop - s, 0x400)):
            if ins.address >= stop:
                break
            if ins.mnemonic == "call" and ins.op_str.startswith("0x"):
                calls.append(int(ins.op_str, 16))
            if ins.mnemonic == "jmp" and ins.op_str == hex(END):
                break
        out.append(dict(block_va=h(s), block_fo=fo(s), default=(s == DEFAULT1), ops=blocks[s],
                        calls=[dict(va=h(c), fo=fo(c)) for c in calls[:6]]))
    return out


recv1 = group(0x94828C, 0x9480C0, 255, 0, 114)
recv2 = group(0x9483D4, 0x94838C, 240, 6, 17)
recv3 = group(0x9484CC, 0x9484C4, 235, 0x13B, 1)

# send names: same table as wo_copnames.py (sub_951F80: opcode-3 -> index 0x952870 -> jump 0x952640)
send = {}
for op in range(3, 3 + 0xFC):
    k = im.read(0x952870 + op - 3, 1)[0]
    tgt = im.d32(0x952640 + 4 * k)
    if tgt is None or im.sec(tgt)[0] != ".text":
        continue
    for ins in im.dis(tgt, 16, 2):
        mm = re.search(r"(0x1[0-9a-f]{6})", ins.op_str)
        if ins.mnemonic in ("mov", "push") and mm:
            b = im.read(int(mm.group(1), 16), 48)
            if b and b.startswith(b"C_"):
                send[op] = b.split(b"\0")[0].decode()
                break

# UI XML names
xml = {}
for m in re.finditer(rb"[A-Za-z0-9_\-]{3,40}\.xml\0", im.data):
    s = m.group()[:-1].decode()
    o = m.start()
    va = next((v + o - p for n, v, vs, p, r in im.secs if p <= o < p + r), None)
    if va is None:
        continue
    refs = []
    for a, ins in im.xrefs(va):
        f = im.func_start(a)
        refs.append(dict(site=h(a), func=h(f), func_fo=fo(f) if f else None))
    # data pointers to the string (name tables)
    ptr = struct.pack("<I", va)
    dptr = [h(next(v + q - p for n, v, vs, p, r in im.secs if p <= q < p + r))
            for q in [mm.start() for mm in re.finditer(re.escape(ptr), im.data)]
            if q % 4 == 0 and q >= 0xD49C00]
    xml.setdefault(s, []).append(dict(va=h(va), fo=f"0x{o:X}", code_refs=refs[:8], data_ptrs=dptr[:4]))

rtti = []
for m in re.finditer(rb"\.\?A[UV][A-Za-z0-9_@?$]{2,120}", im.data):
    s = m.group().decode()
    if re.search(r"UI|Window|Wnd|Dlg|Dialog", s):
        o = m.start()
        va = next((v + o - p for n, v, vs, p, r in im.secs if p <= o < p + r), None)
        rtti.append(dict(name=s, va=h(va), fo=f"0x{o:X}"))

out = dict(bin_sha256=im.data and __import__("hashlib").sha256(im.data).hexdigest(), names={str(k): v for k, v in names.items()},
           recv1=recv1, recv2=recv2, recv3=recv3, send={str(k): v for k, v in send.items()}, xml=xml, rtti=rtti)
p = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "scratch/t202/inventory.json")
p.parent.mkdir(parents=True, exist_ok=True)
p.write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding="utf-8")
print("names", len(names), "recv1 blocks", len(recv1), "recv2", len(recv2), "recv3", len(recv3),
      "send", len(send), "xml", len(xml), "rtti", len(rtti))
