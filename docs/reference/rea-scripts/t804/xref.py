"""T341 (#804) read-only: find code referencing an absolute address. Usage: py -3 -I xref.py BIN VA ..."""
import sys, struct, pefile
pe = pefile.PE(sys.argv[1], fast_load=True)
data = pe.__data__
base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Characteristics & 0x20000000]
for a in sys.argv[2:]:
    va = int(a, 16); pat = struct.pack("<I", va)
    print(f"=== refs to {va:#x}")
    for s in text:
        lo = s.PointerToRawData; hi = lo + s.SizeOfRawData
        j = data.find(pat, lo, hi)
        while j != -1:
            print(f"  {base + s.VirtualAddress + j - lo:#x}")
            j = data.find(pat, j + 1, hi)
