"""唯讀解出 Text.pak 的 music.def / envsound.def（T171／#445）。

需要 Python 3.11+、pycryptodome；只寫本 checkout 的 scratch/，不複製容器。
用法：python -B -X utf8 sound_def_extract.py CLIENT --out scratch/issue-445/decoded
可加 --bin TW1608262200.bin，核對已分析的 BIN SHA 與固定格式金鑰位置。
格式及靜態證據見 ../sound.md 第 6 節；本工具不執行 BIN、不修改客戶端。
"""

import argparse
import hashlib
import json
from pathlib import Path
import stat
import struct

from Crypto.Cipher import DES


ROOT = Path(__file__).resolve().parents[3]
# 客戶端容器格式常數，並非帳號憑證。TW16 VA 0x117EFF8。
KEY = bytes.fromhex("7E214023255E243C")
BIN_SHA = "e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2"
TARGETS = ("music.def", "envsound.def")
MARKERS = {"music.def": b"System Setting Begin", "envsound.def": b";Sonud Group"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def file_digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def transform(data, *, encrypt=False):
    """完整 8-byte 區塊做 DES ECB；尾端原樣保留，不加／去 padding。"""
    end = len(data) // 8 * 8
    cipher = DES.new(KEY, DES.MODE_ECB)
    operation = cipher.encrypt if encrypt else cipher.decrypt
    return operation(data[:end]) + data[end:]


def output_directory(requested, client):
    # 不允許 junction/symlink 把寫入導向封存、原客戶端或其他 checkout。
    path = Path(requested).absolute()
    scratch = ROOT / "scratch"
    require(path.is_relative_to(scratch), "輸出必須在本 checkout 的 scratch/ 下")
    for part in (path, *path.parents):
        if part == ROOT:
            break
        if part.exists() or part.is_symlink():
            attrs = part.lstat()
            require(not stat.S_ISLNK(attrs.st_mode)
                    and not getattr(attrs, "st_file_attributes", 0) & 0x400,
                    f"拒絕輸出路徑中的 reparse point：{part}")
    path = path.resolve()
    require(path.is_relative_to(scratch.resolve()) and path != scratch.resolve(),
            "請指定 scratch/ 內的任務子目錄")
    require(not path.is_relative_to(client.resolve()), "輸出不可位於客戶端目錄")
    return path


def write_new_or_identical(path, data):
    if path.exists() or path.is_symlink():
        attrs = path.lstat()
        require(stat.S_ISREG(attrs.st_mode) and attrs.st_nlink == 1
                and not getattr(attrs, "st_file_attributes", 0) & 0x400,
                f"拒絕非一般檔案或硬連結：{path}")
        require(path.read_bytes() == data, f"既有輸出不同，拒絕覆寫：{path}")
        return
    with path.open("xb") as stream:
        stream.write(data)


def extract(client, out, bin_path=None):
    out = output_directory(out, client)
    idx, pak = client / "Text.idx", client / "Text.pak"
    sources = [idx, pak] + ([bin_path] if bin_path else [])
    before = {str(p.resolve()): file_digest(p) for p in sources}
    if bin_path:
        require(before[str(bin_path.resolve())] == BIN_SHA, "BIN SHA 不符 TW16 基準")
        with bin_path.open("rb") as stream:
            stream.seek(0xD7DBF8)  # File Offset；非 .text，勿套用 VA - 0x400C00。
            require(stream.read(8) == KEY, "BIN 固定格式金鑰位置不符")

    raw = idx.read_bytes()
    require(len(raw) >= 4 and raw[:4] != b"_IDX", "僅支援 count + DES plain28 索引")
    count, = struct.unpack_from("<I", raw)
    require(0 < count <= 1_000_000 and len(raw) == 4 + 28 * count,
            "IDX count／長度不符")
    rows = transform(raw[4:])
    require(transform(rows, encrypt=True) == raw[4:], "IDX round-trip 不符")
    selected = {}
    pak_size = pak.stat().st_size
    for number, (offset, name_bytes, size) in enumerate(struct.iter_unpack("<I20sI", rows)):
        name = name_bytes.split(b"\0", 1)[0]
        require(name and all(32 <= c < 127 for c in name),
                f"IDX record {number} 名稱無效")
        require(offset + size <= pak_size, f"IDX record {number} 超出 PAK")
        name = name.decode("ascii").lower()
        if name in TARGETS:
            require(name not in selected, f"目標名稱重複：{name}")
            require(0 < size <= 4 * 1024 * 1024, f"目標長度異常：{name}")
            selected[name] = (number, offset, size)
    require(set(selected) == set(TARGETS), "Text.idx 未包含全部目標")

    outputs, entries = {}, {}
    with pak.open("rb") as stream:
        for name in TARGETS:
            number, offset, size = selected[name]
            stream.seek(offset)
            packed = stream.read(size)
            require(len(packed) == size, f"PAK payload 截斷：{name}")
            plain = transform(packed)
            require(plain.startswith(MARKERS[name]) and b"\0" not in plain,
                    f"不符已驗證的定義檔格式：{name}")
            require(transform(plain, encrypt=True) == packed, f"payload round-trip 不符：{name}")
            outputs[name] = plain  # 保留原編碼及 CRLF，沒有重新序列化內容。
            entries[name] = {
                "record_zero_based": number, "idx_record_file_offset": 4 + 28 * number,
                "pak_file_offset": offset, "bytes": size, "unchanged_tail_bytes": size % 8,
                "packed_sha256": digest(packed), "decoded_sha256": digest(plain),
                "round_trip": True,
            }
    after = {str(p.resolve()): file_digest(p) for p in sources}
    require(before == after, "讀取期間來源 SHA 改變，拒絕交付")
    report = {
        "format": "Text DES-ECB plain28; no padding; partial final block unchanged",
        "index_count": count, "sources_sha256": before, "sources_unchanged": True,
        "bin_verified": bool(bin_path), "entries": entries,
    }
    outputs["manifest.json"] = (json.dumps(report, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    out.mkdir(parents=True, exist_ok=True)
    for name, data in outputs.items():
        write_new_or_identical(out / name, data)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("client", type=Path)
    parser.add_argument("--out", type=Path, default=ROOT / "scratch/issue-445/decoded")
    parser.add_argument("--bin", dest="bin_path", type=Path)
    args = parser.parse_args()
    try:
        report = extract(args.client, args.out, args.bin_path)
    except (OSError, ValueError) as error:
        parser.exit(1, f"失敗：{error}\n")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
