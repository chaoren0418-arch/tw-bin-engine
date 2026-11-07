"""Cross-version parser fingerprint: raw search for the xor immediates, no PE assumptions."""
import hashlib, pathlib, re

F = {
    "TW1608262200": r"D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin",
    "TW13081901.unpacked": r"D:\lineage開發\登入器bin資源\TW13081901bin拆解\TW13081901.unpacked.exe",
    "LiTo.unpacked-oep-current": r"D:\lineage開發\3-lito開發\lito登入器\舊版分析與驗收\runtime\LiTo.unpacked-oep-current.exe",
    "Lin.bin(VMP)": r"D:\lineage開發\登入器bin資源\Lin.bin",
    "LinR.bin": r"D:\lineage開發\登入器bin資源\LinR.bin拆解\LinR.bin",
}
for n, p in F.items():
    b = pathlib.Path(p).read_bytes()
    a = [m.start() for m in re.finditer(re.escape(b"\x51\x19\x00\x00"), b)]
    c = [m.start() for m in re.finditer(re.escape(b"\x12\x18\x00\x00"), b)]
    near = [x for x in a if any(0 < y - x < 0x1000 for y in c)]
    print(n, len(b), hashlib.sha256(b).hexdigest()[:16], "imm1951", len(a), "imm1812", len(c), "paired", len(near), [hex(x) for x in near[:4]],
          "strings", {k.decode(): b.count(k) for k in (b"list.spr", b"loadSprites", b"LoadSprite Start")})
