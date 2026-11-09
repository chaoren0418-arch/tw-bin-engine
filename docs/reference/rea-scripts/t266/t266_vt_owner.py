"""Read-only (T266 / #627): for each slot address (VA of a vtable entry) and slot index, resolve the owning vtable's
MSVC RTTI class name (vtable[-1] = CompleteObjectLocator, COL+12 = TypeDescriptor, TD+8 = mangled name).
Usage: py -3 -I t266_vt_owner.py <TW1608262200.bin> <slot index> <slot VA> [<slot VA> ...]
Section mapping: .rdata FO = VA - 0x401400 (TW16, see gap-matrix section 0)."""
import struct
import sys

data = open(sys.argv[1], "rb").read()
slot = int(sys.argv[2])


def fo(va):
    if 0x114B000 <= va < 0x135C000:
        return va - 0x401400
    if va >= 0x135C000:
        return va - 0x401800
    return va - 0x400C00


def d(va):
    return struct.unpack_from("<I", data, fo(va))[0]


for tok in sys.argv[3:]:
    s = int(tok, 16)
    vt = s - 4 * slot
    try:
        col = d(vt - 4)
        td = d(col + 12)
        name = data[fo(td + 8):fo(td + 8) + 80].split(b"\0")[0].decode("latin1")
    except Exception as e:  # not a vtable start with RTTI
        name = f"? ({e})"
    print(f"slot {tok} -> vtable {vt:#x} -> {name}")
