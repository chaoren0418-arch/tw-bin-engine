"""T167：唯讀核對本機光源／雨圖；stdout 只輸出定位與雜湊，不輸出圖像。

python -B -X utf8 t167_resources.py <client-root> [--xml-out scratch/.../SurfList.xml]
需要 pycryptodome；沿用 #429 經 TW16 靜態驗證的 XML 解密函式。
解密 XML 預設只在記憶體；選用輸出強制限制於本 worktree 的 scratch/。
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

from ui_xml_decrypt import decrypt


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def records(path):
    data = path.read_bytes()
    count, = struct.unpack_from("<I", data)
    if len(data) != 4 + count * 28:
        raise ValueError(f"索引長度不符：{path.name}")
    for ordinal in range(count):
        at = 4 + ordinal * 28
        offset, name, size = struct.unpack_from("<I20sI", data, at)
        yield {
            "idx": path.name, "ordinal_zero_based": ordinal,
            "idx_file_offset": hex(at), "name": name.split(b"\0")[0].decode("ascii"),
            "pak": path.with_suffix(".pak").name,
            "pak_file_offset": hex(offset), "size": size,
        }


def payload(root, entry):
    with (root / entry["pak"]).open("rb") as stream:
        offset = int(entry["pak_file_offset"], 16)
        if offset + entry["size"] > stream.seek(0, 2):
            raise ValueError("pak 條目越界")
        stream.seek(offset)
        data = stream.read(entry["size"])
    if len(data) != entry["size"]:
        raise ValueError("pak 短讀")
    entry["sha256"] = sha(data)
    return data


def scratch_xml(path, data):
    scratch = Path(__file__).resolve().parents[3] / "scratch"
    target = Path(path).absolute()
    # 限制語法與解析後的路徑，並拒絕 junction／symlink，避免改到外部原件。
    if not target.is_relative_to(scratch) or not target.resolve().is_relative_to(scratch.resolve()):
        raise ValueError("XML 輸出必須在本 worktree 的 scratch/")
    for parent in (target, *target.parents):
        if parent.is_symlink() or parent.is_junction():
            raise ValueError("XML 輸出路徑不可含連結")
        if parent == scratch:
            break
    target.parent.mkdir(parents=True, exist_ok=True)
    # 已存在時只接受同內容；不覆寫，也不刪除。
    if target.exists():
        if target.read_bytes() != data:
            raise ValueError("輸出已存在且內容不同")
    else:
        with target.open("xb") as stream:
            stream.write(data)


def audit(root, xml_out=None):
    wanted = {"source.lmp", "surflist.xml", *(f"{i}.img" for i in range(924, 928))}
    found = {name: [] for name in wanted}
    indexes = sorted(root.glob("Sprite*.idx")) + [root / "Tile.idx"]
    for idx in indexes:
        for entry in records(idx):
            key = entry["name"].casefold()
            if key in found:
                found[key].append(entry)
    if any(len(hits) != 1 for hits in found.values()):
        raise ValueError(f"條目缺少或不唯一：{found}")
    result = {"client_root": str(root), "indexes_scanned": [p.name for p in indexes],
              "entries": [], "files": {}, "light_slots": [], "rain_mapping": []}
    for name in sorted(found):
        entry = found[name][0]
        data = payload(root, entry)
        result["entries"].append(entry)
        for filename in (entry["idx"], entry["pak"]):
            if filename not in result["files"]:
                path = root / filename
                result["files"][filename] = {"size": path.stat().st_size, "sha256": file_sha(path)}
        if name == "surflist.xml":
            if data[:4] != b"X?xm":
                raise ValueError("非本次驗證的加密 XML 標頭")
            plain = decrypt(data)
            tree = ET.fromstring(plain)
            if tree.tag != "surfs":
                raise ValueError("XML 根節點不是 surfs")
            mapping = {int(node.attrib["id"]): node.attrib for node in tree.findall("surf")}
            if len(mapping) != len(tree.findall("surf")):
                raise ValueError("Surf ID 重複")
            entry["decrypted_sha256"] = sha(plain)
            entry["surf_count"] = len(mapping)
            entry["negative_rain_ids_present"] = [i for i in range(-100, -96) if i in mapping]
            for i in range(924, 928):
                if mapping[i].get("file") != str(i):
                    raise ValueError("雨圖 XML 對應不符")
                result["rain_mapping"].append({"surf_id": i, "file": f"{i}.img"})
            if xml_out:
                scratch_xml(xml_out, plain)
        elif name == "source.lmp":
            if len(data) < 192:
                raise ValueError("光源標頭短讀")
            for i in range(16):
                offset, = struct.unpack_from("<I", data, i * 4)
                width, height = struct.unpack_from("<II", data, 64 + i * 8)
                size = width * height
                if (size and offset < 192) or offset + size > len(data):
                    raise ValueError("光源圖越界")
                pixels = data[offset:offset + size]
                result["light_slots"].append({"slot": i, "lmp_offset": hex(offset),
                    "width": width, "height": height, "size": size,
                    "min": min(pixels, default=0), "max": max(pixels, default=0), "sha256": sha(pixels)})
        else:
            width, height, flag, key = struct.unpack_from("<4H", data)
            if not width or not height or len(data) != 8 + 2 * width * height:
                raise ValueError("IMG 尺寸不符")
            entry.update(width=width, height=height, flag=flag, color_key=key)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("client_root", type=Path)
    parser.add_argument("--xml-out")
    args = parser.parse_args()
    print(json.dumps(audit(args.client_root.resolve(), args.xml_out), ensure_ascii=False, indent=2))
