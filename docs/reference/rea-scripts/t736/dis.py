"""T309 (#736) read-only: disassemble TW1608262200.bin functions. Usage: py -3 -I dis.py VA [count] ..."""
import sys, pefile, capstone
BIN = r"D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin"
pe = pefile.PE(BIN, fast_load=True)
data = pe.__data__
base = pe.OPTIONAL_HEADER.ImageBase
def fo(va):
    rva = va - base
    for s in pe.sections:
        if s.VirtualAddress <= rva < s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData):
            return rva - s.VirtualAddress + s.PointerToRawData
    raise ValueError(hex(va))
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
args = sys.argv[1:]
i = 0
while i < len(args):
    va = int(args[i], 16); n = 120
    if i + 1 < len(args) and not args[i+1].lower().startswith("0x"):
        n = int(args[i+1]); i += 1
    i += 1
    off = fo(va)
    print(f"=== {va:#x} FO {off:#x}")
    c = 0
    for ins in md.disasm(data[off:off+n*16], va):
        print(f"{ins.address:#x}: {ins.mnemonic} {ins.op_str}")
        c += 1
        if c >= n or (ins.mnemonic == "ret" and c > 3 and n <= 0):
            break
