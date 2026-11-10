"""T341 (#804) read-only: disassemble TW1608262200.bin. Usage: py -3 -I dis.py BIN VA [count] ..."""
import sys, pefile, capstone
pe = pefile.PE(sys.argv[1], fast_load=True)
data = pe.__data__
base = pe.OPTIONAL_HEADER.ImageBase
def fo(va):
    rva = va - base
    for s in pe.sections:
        if s.VirtualAddress <= rva < s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData):
            return rva - s.VirtualAddress + s.PointerToRawData
    raise ValueError(hex(va))
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
args = sys.argv[2:]
i = 0
while i < len(args):
    va = int(args[i], 16); n = 60
    if i + 1 < len(args) and not args[i+1].lower().startswith("0x"):
        n = int(args[i+1]); i += 1
    i += 1
    off = fo(va)
    print(f"=== {va:#x} FO {off:#x}")
    for c, ins in enumerate(md.disasm(data[off:off+n*16], va)):
        if c >= n: break
        print(f"{ins.address:#x}: {ins.mnemonic} {ins.op_str}")
