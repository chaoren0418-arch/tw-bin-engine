"""Read-only: decrypt the UI XML files stored in the client's Tile.pak (Issue #429).

Format (verified in TW1608262200.bin):
  sub_B3D710 (VA 0xB3D710, File Offset 0x73CB10): decrypt(buf+4, len-4), then buf[0] = '<'.
  sub_A9FA30 (VA 0xA9FA30, File Offset 0x69EE30): AES-128 key = first 16 bytes of a 32-byte stack
    constant, IV = 16-byte stack constant; sub_4D5EB0 = key schedule (128 bits),
    sub_4D8FB0 = CBC decrypt, trailing <16 bytes are XORed with the running IV (no block op).

Usage: py -3 -I ui_xml_decrypt.py <client dir> <output dir in scratch/>
Writes decrypted copies only to the output dir; never writes to the client directory.
Requires pycryptodome.
"""
import hashlib
import os
import struct
import sys

from Crypto.Cipher import AES

KEY = bytes(x & 0xFF for x in [-36, -124, 1, 33, 42, 64, 32, 10, -35, 37, -71, -89, 13, -71, -55, 78])
IV = b">\tx" + bytes(x & 0xFF for x in [-86, -60, -43, 48, 99, 48, 12, 95, -102, 0x80, 127]) + b'"F'


def decrypt(blob: bytes) -> bytes:
    body = blob[4:]
    ecb = AES.new(KEY, AES.MODE_ECB)
    iv, out = IV, bytearray()
    full = len(body) // 16 * 16
    for i in range(0, full, 16):
        c = body[i:i + 16]
        out += bytes(a ^ b for a, b in zip(ecb.decrypt(c), iv))
        iv = c
    out += bytes(a ^ b for a, b in zip(body[full:], iv))
    return b"<" + blob[1:4] + bytes(out)


def entries(idx_path):
    b = open(idx_path, "rb").read()
    for i in range(4, len(b) - 27, 28):
        off, = struct.unpack_from("<I", b, i)
        name = b[i + 4:i + 24].split(b"\0")[0].decode("latin1")
        size, = struct.unpack_from("<I", b, i + 24)
        yield off, name, size


def main():
    client, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(client, "Tile.pak"), "rb") as pak:
        for off, name, size in entries(os.path.join(client, "Tile.idx")):
            if not name.lower().endswith(".xml"):
                continue
            pak.seek(off)
            blob = pak.read(size)
            plain = decrypt(blob) if blob[:4] == b"X?xm" else blob
            open(os.path.join(out, name), "wb").write(plain)
            print(f"{name}\tpak_off={off:#x}\tsize={size}\tsha256={hashlib.sha256(blob).hexdigest()[:16]}\t{plain[:30]!r}")


if __name__ == "__main__":
    main()
