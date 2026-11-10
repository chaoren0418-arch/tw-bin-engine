"""T309 (#736) read-only: find E8 rel32 call sites to given VAs. Usage: py -3 -I callers.py VA [VA...]"""
import sys, struct, pefile
BIN = r"D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin"
pe = pefile.PE(BIN, fast_load=True)
data = pe.__data__; base = pe.OPTIONAL_HEADER.ImageBase
text = next(s for s in pe.sections if s.Name.startswith(b".text"))
t0 = text.PointerToRawData; seg = data[t0:t0+text.SizeOfRawData]; tva = base + text.VirtualAddress
targets = [int(a, 16) for a in sys.argv[1:]]
for i in range(len(seg) - 5):
    if seg[i] != 0xE8: continue
    rel = struct.unpack_from("<i", seg, i + 1)[0]
    dst = tva + i + 5 + rel
    if dst in targets: print(f"{tva+i:#x} -> {dst:#x}")
