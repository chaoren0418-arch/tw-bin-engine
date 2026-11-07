"""T167：以已鎖定 BIN 的機器碼核對雨圖 ID，避免 IDA char 型別截斷。

python -B -X utf8 t167_static.py [--tw16 PATH] [--tw13 PATH] [--lito PATH]
依賴 pefile、capstone；只讀來源，stdout 輸出 VA／File Offset 與短指令。
完整 XML 解密與雪花函式可用 t167_targets.txt 配合 run_ida.ps1 重跑。
"""
import argparse
import json
from pathlib import Path

from nat import BIN, SHA, Img, fmt


def inspect(path, digest, sites):
    im = Img(path, digest)
    result = {"path": str(path), "sha256": digest, "sites": []}
    for va, expected, meaning in sites:
        expected = bytes.fromhex(expected)
        actual = im.read(va, len(expected))
        if actual != expected:
            raise ValueError(f"機器碼不符：{va:#x}")
        result["sites"].append({"va": hex(va), "file_offset": hex(im.off(va)),
            "meaning": meaning, "instructions": [fmt(i) for i in im.dis(va, len(expected))]})
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tw16", type=Path, default=Path(BIN))
    parser.add_argument("--tw13", type=Path, default=Path(
        r"D:\lineage開發\登入器bin資源\TW13081901bin拆解\TW13081901.unpacked.exe"))
    parser.add_argument("--lito", type=Path, default=Path(
        r"D:\lineage開發\3-lito開發\lito登入器\舊版分析與驗收\runtime\LiTo.unpacked-oep-current.exe"))
    args = parser.parse_args()
    result = [
        inspect(args.tw16, SHA, [
            (0xF0E66D, "81c29c03000052", "層 0：32-bit 序號奇偶 + 924，push edx"),
            (0xF0E6AE, "059e03000050", "層 1：32-bit 序號奇偶 + 926，push eax"),
            (0xE538E3, "8b450850", "Surf 查詢傳入完整 32-bit 參數，並非 char"),
            (0xF0A501, "8b55f0a1384654018b0d38465401030c90", "LMP 基址加 32-bit 槽位偏移"),
        ]),
        inspect(args.tw13, "653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4", [
            (0x4ED2CB, "81c19c03000051", "雨層 0：924 + 奇偶"),
            (0x4ED30A, "81c29e03000052", "雨層 1：926 + 奇偶"),
        ]),
        inspect(args.lito, "4271d92fb2b9a2fa8d118dde0767f6d8a67a896227bcc24127438fcb5c2ab6e9", [
            (0x486DDB, "059c030000", "雨層 0：924 + 奇偶"),
            (0x486D5E, "059e030000", "雨層 1：926 + 奇偶"),
            (0x486DF1, "8b3481", "以完整 eax 圖號索引 Surf 指標表"),
        ]),
    ]
    print(json.dumps(result, ensure_ascii=False, indent=2))
