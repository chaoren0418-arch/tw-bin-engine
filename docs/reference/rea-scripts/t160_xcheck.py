"""T160 (#428) cross-version fingerprints for actions / names / polymorph. Read-only; never writes inputs.

Usage: py -3 -I docs/reference/rea-scripts/t160_xcheck.py [out.json]
Scans executable sections of TW1608262200.bin and TW13081901.unpacked.exe with Capstone and reports
windows whose immediates match each rule. Results are evidence for docs/reference/actions-and-names.md.
"""
import hashlib, json, pathlib, struct, sys
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
from capstone.x86 import X86_OP_IMM

L = "D:\\lineage\u958b\u767c\\\u767b\u5165\u5668bin\u8cc7\u6e90"
FILES = {
    "TW1608262200": (L + "\\TW1608262200.bin\u62c6\u89e3\\TW1608262200.bin",
                     "e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2"),
    "TW13081901.unpacked": (L + "\\TW13081901bin\u62c6\u89e3\\TW13081901.unpacked.exe",
                            "653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4"),
}
# rule: name -> (window bytes, set of immediates that must all appear in the window)
RULES = {
    "emote_actions_66_69": (0x180, {0x42, 0x43, 0x44, 0x45}),
    "class_sprite_list": (0x100, {61, 48, 138, 37, 2786, 2796, 734, 1186}),
    "horse_helmet_6080_6094": (0x20, {6080, 6094}),
    "lawful_color_500_7fff_7c00": (0x120, {500, 0x7FFF, 0x7C00, 31}),
    "pink_timer_127_x1000": (0x80, {127, 1000}),
    "emblem_blit_16x12_928_1856": (0x80, {928, 1856, 12, 16}),
}
STRINGS = [b"Action_SocialAction1", b"Emblem%d.dat", b"font\\kor.fnt", b"MingLiU",
           b"Sprite file %d-%d.spr doesn't exist", b"Direction Error %d (Changed 0)"]
PALETTE_HEAD = struct.pack("<6I", 0x0000, 0x001F, 0x03E0, 0x7C00, 0x509E, 0x3421)


def scan(path, sha):
    data = pathlib.Path(path).read_bytes()
    got = hashlib.sha256(data).hexdigest()
    assert got == sha, f"unexpected identity {path}: {got}"
    pe = pefile.PE(data=data, fast_load=True)
    base = pe.OPTIONAL_HEADER.ImageBase
    md = Cs(CS_ARCH_X86, CS_MODE_32); md.detail = True; md.skipdata = True
    imms = []  # (va, imm)
    for s in pe.sections:
        if not s.Characteristics & 0x20000000:
            continue
        va0 = base + s.VirtualAddress
        raw = data[s.PointerToRawData:s.PointerToRawData + s.SizeOfRawData]
        for ins in md.disasm(raw, va0):
            if ins.id == 0:
                continue
            for op in getattr(ins, "operands", []):
                if op.type == X86_OP_IMM:
                    imms.append((ins.address, op.imm & 0xFFFFFFFF))
    res = {"sha256": got, "imagebase": hex(base), "rules": {}, "strings": {}, "palette": []}
    for name, (win, want) in RULES.items():
        hits, j, last = [], 0, -1
        for i in range(len(imms)):
            while imms[j][0] < imms[i][0] - win:
                j += 1
            seen = {v for _, v in imms[j:i + 1] if v in want}
            if seen == want and imms[j][0] > last:
                hits.append(hex(imms[j][0])); last = imms[i][0]
        res["rules"][name] = hits[:12]
    for s in STRINGS:
        res["strings"][s.decode()] = data.count(s)
    off = data.find(PALETTE_HEAD)
    while off != -1:
        sec = next((x for x in pe.sections if x.PointerToRawData <= off < x.PointerToRawData + x.SizeOfRawData), None)
        va = base + sec.VirtualAddress + off - sec.PointerToRawData if sec else None
        vals = struct.unpack_from("<64I", data, off)
        res["palette"].append({"file_offset": hex(off), "va": hex(va) if va else None,
                               "idx14": hex(vals[14]), "idx42": hex(vals[42]), "idx54": hex(vals[54])})
        off = data.find(PALETTE_HEAD, off + 4)
    return res


if __name__ == "__main__":
    out = {k: scan(*v) for k, v in FILES.items()}
    text = json.dumps(out, indent=1, ensure_ascii=False)
    if len(sys.argv) > 1:
        pathlib.Path(sys.argv[1]).write_text(text, encoding="utf-8")
    print(text)
