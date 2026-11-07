"""Read-only helpers over a fixed x86 PE. Never writes to the input."""
import hashlib, pathlib, struct
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

BIN = r"D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin"
SHA = "e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2"


class Img:
    def __init__(self, path=BIN, sha=SHA):
        self.data = pathlib.Path(path).read_bytes()
        if sha:
            assert hashlib.sha256(self.data).hexdigest() == sha, "unexpected BIN identity"
        self.pe = pefile.PE(data=self.data, fast_load=True)
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        self.secs = [(s.Name.rstrip(b"\0").decode("latin1"), self.base + s.VirtualAddress,
                      s.Misc_VirtualSize, s.PointerToRawData, s.SizeOfRawData) for s in self.pe.sections]
        self.md = Cs(CS_ARCH_X86, CS_MODE_32)
        self.md.detail = True

    def off(self, va):
        for n, v, vs, p, r in self.secs:
            if v <= va < v + max(vs, r):
                return (p + va - v) if va - v < r else None
        return None

    def sec(self, va):
        for n, v, vs, p, r in self.secs:
            if v <= va < v + max(vs, r):
                return n, ("raw" if va - v < r else "bss")
        return None, None

    def read(self, va, n):
        o = self.off(va)
        return None if o is None else self.data[o:o + n]

    def d32(self, va):
        b = self.read(va, 4)
        return None if b is None else struct.unpack("<I", b)[0]

    def dis(self, va, n=None, count=0):
        b = self.read(va, n or 4096)
        return list(self.md.disasm(b, va, count)) if count else list(self.md.disasm(b, va))

    def text(self):
        for n, v, vs, p, r in self.secs:
            if n == ".text":
                return v, self.data[p:p + r]

    def xrefs(self, target):
        """Absolute-operand references in .text; returns decoded instructions containing the address."""
        v, t = self.text()
        pat = struct.pack("<I", target)
        out, i = [], 0
        while True:
            i = t.find(pat, i)
            if i < 0:
                return out
            hit = None
            for back in range(1, 8):
                start = i - back
                ins = list(self.md.disasm(t[start:start + 15], v + start, 1))
                if ins and ins[0].size >= back + 4 and ins[0].size == back + 4 + (ins[0].size - back - 4):
                    if ins[0].address + ins[0].size >= v + i + 4 and pat.hex() in ins[0].bytes.hex():
                        hit = ins[0]
                        if ins[0].mnemonic != "add" or ins[0].op_str.find("byte ptr [eax]") < 0:
                            break
            out.append((v + i, hit))
            i += 1

    def func_start(self, va, limit=0x6000):
        """Heuristic: nearest preceding CC/C3-padded prologue."""
        for a in range(va, va - limit, -1):
            b = self.read(a, 3)
            p = self.read(a - 1, 1)
            if b and b[:3] == b"\x55\x8b\xec" and p in (b"\xcc", b"\xc3", b"\x90"):
                return a
        return None


def fmt(i):
    return f"{i.address:08X}  {i.bytes.hex():<20} {i.mnemonic} {i.op_str}"
