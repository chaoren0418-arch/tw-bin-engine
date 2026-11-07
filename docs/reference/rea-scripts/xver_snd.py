"""#431 sound cross-check: take instruction windows from TW1608262200.bin at known VAs and look for the same
byte shapes in another sample (e.g. TW13081901.unpacked.exe). Read-only; paths come from argv so that
non-ASCII directories survive (the older scripts in this folder have mangled hard-coded paths).

usage: python -I xver_snd.py <TW1608262200.bin> <other.exe>
"""
import hashlib, re, struct, sys, pathlib

TW16_SHA = "e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2"
TEXT_DELTA = 0x400C00  # .text: File Offset = VA - 0x400C00 (#417)

# name: (VA, length, note).  Windows are short and dominated by immediates; call rel32 and
# ebp-relative displacements are wildcarded because frame layouts differ between builds.
WIN = {
    "dist_dead_store_127_minus_7":   (0xA3FB43, 10, "imul eax,7; mov edx,7Fh; sub edx,eax"),
    "emitter_ext_64_minus_63":       (0xA3FD3C, 18, "imul eax,3Fh; cdq; mov ecx,0Eh; idiv ecx; mov edx,40h; sub edx,eax"),
    "emitter_vol_100_minus_14d2_20": (0xA3FD5F, 18, "imul eax,0Eh; cdq; mov ecx,14h; idiv ecx; mov edx,64h; sub edx,eax"),
    "manhattan_clamp_14":            (0xA3FD20, 22, "add edx,..; cmp edx,0Eh; jle; mov ..,0Eh; mov ..,0"),
    "cooldown_plus_200":             (0xA060AB, 9,  "mov edx,[ebp-8]; add edx,0C8h"),
}


def mask(code):
    """Regex that wildcards E8 rel32 and [ebp+disp8] bytes."""
    out, i = b"", 0
    while i < len(code):
        c = code[i]
        if c == 0xE8 and i + 5 <= len(code):
            out += b"\xE8...."; i += 5; continue
        if c in (0x8B, 0x89, 0x03, 0x2B, 0x0F) and i + 2 < len(code) and (code[i + 1] & 0xC7) == 0x45:
            out += re.escape(bytes([c, code[i + 1]])) + b"."; i += 3; continue
        if c == 0xC7 and i + 2 < len(code) and code[i + 1] == 0x45:
            out += b"\xC7\x45." ; i += 3; continue
        if c == 0x7E and i + 1 < len(code):  # jle rel8
            out += b"\x7E."; i += 2; continue
        out += re.escape(bytes([c])); i += 1
    return re.compile(out, re.S)


def main():
    a = pathlib.Path(sys.argv[1]).read_bytes()
    assert hashlib.sha256(a).hexdigest() == TW16_SHA, "unexpected TW16 identity"
    b = pathlib.Path(sys.argv[2]).read_bytes()
    print("other sha256", hashlib.sha256(b).hexdigest())
    for name, (va, n, note) in WIN.items():
        fo = va - TEXT_DELTA
        code = a[fo:fo + n]
        rx = mask(code)
        self_hits = [m.start() for m in rx.finditer(a)]
        hits = [m.start() for m in rx.finditer(b)]
        print(f"{name}: VA {va:#x} FO {fo:#x} bytes {code.hex()} | tw16 hits {len(self_hits)} | other hits {len(hits)} {[hex(h) for h in hits[:4]]}  ({note})")


if __name__ == "__main__":
    main()
