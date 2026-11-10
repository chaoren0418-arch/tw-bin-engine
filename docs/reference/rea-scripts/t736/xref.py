"""T309 (#736) read-only: find instructions in .text that reference a 32-bit absolute address.
Usage: py -3 -I xref.py ADDR [ADDR...]  -> prints containing instruction (by backward resync)."""
import sys, struct, pefile, capstone
BIN = r"D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin"
pe = pefile.PE(BIN, fast_load=True)
data = pe.__data__
base = pe.OPTIONAL_HEADER.ImageBase
text = next(s for s in pe.sections if s.Name.startswith(b".text"))
t0 = text.PointerToRawData; tl = text.SizeOfRawData; tva = base + text.VirtualAddress
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
for a in sys.argv[1:]:
    addr = int(a, 16); pat = struct.pack("<I", addr)
    seg = data[t0:t0+tl]; pos = 0
    print(f"### {addr:#x}")
    while True:
        k = seg.find(pat, pos)
        if k < 0: break
        pos = k + 1
        hit = None
        for back in range(1, 9):
            start = k - back
            ins = next(md.disasm(seg[start:start+16], tva + start), None)
            if ins and ins.size >= back + 4 and start + ins.size >= k + 4:
                hit = ins; break
        if hit: print(f"{hit.address:#x}: {hit.mnemonic} {hit.op_str}")
        else: print(f"{tva+k:#x}: (raw)")
