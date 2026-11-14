# T1242（#1242）：比對 SelectUI／CreateUI 引用的 surf 在本機 0-客戶端與參考客戶端 kor25 各解析到哪個 .img（唯讀）。
# 依 HashFileManager 順序（"<F>c.img" 在 Sprite{h}、Sprite，再 "<F>.img" 在 Sprite{h}、Sprite；h = 檔名 byte 和 % 16）。
# 只讀 .idx／.pak；輸出寫到 scratch/i1242/（TSV 與每張圖的 PNG 預覽，不進 git）。
# 用法（repo 根目錄）：py -3 -I docs/reference/rea-scripts/pak/surf_compare.py <SelectUI.dec.xml> <CreateUI.dec.xml>
import hashlib, os, re, struct, sys, zlib

MAIN = r'D:\lineage開發\0-客戶端'
KOR25 = r'D:\381懶人包kor25(原)'
LY = r'D:\天堂參考用客戶單\雷雨天堂'
T89 = r'D:\天堂參考用客戶單\89天堂'
CLIENTS = (('main', MAIN), ('kor25', KOR25), ('ly', LY), ('t89', T89))
OUT = os.path.join('scratch', 'i1242')


def load_index(root, a):
    name = 'Sprite' if a == 0 else 'Sprite%02d' % (a - 1)
    idx = open(os.path.join(root, name + '.idx'), 'rb').read()
    n = struct.unpack_from('<I', idx, 0)[0]
    table = {}
    for i in range(n):
        at = 4 + i * 28
        off = struct.unpack_from('<I', idx, at)[0]
        raw = idx[at + 4:at + 24].split(b'\0')[0]
        size = struct.unpack_from('<I', idx, at + 24)[0]
        try:
            table.setdefault(raw.decode('ascii').lower(), (off, size))
        except UnicodeDecodeError:
            pass
    return name, table


INDEX = {}


def find(root, file):
    for cand in (file + 'c.img', file + '.img'):
        h = sum(cand.encode()) % 16
        for a in (1 + h, 0):
            key = (root, a)
            if key not in INDEX:
                INDEX[key] = load_index(root, a)
            name, table = INDEX[key]
            e = table.get(cand.lower())
            if e:
                with open(os.path.join(root, name + '.pak'), 'rb') as f:
                    f.seek(e[0])
                    return name + '/' + cand, f.read(e[1])
    return None, None


def png(path, data):
    w, h, flags, key = struct.unpack_from('<HHHH', data, 0)
    rows = []
    for y in range(h):
        row = bytearray([0])
        for x in range(w):
            p = struct.unpack_from('<H', data, 8 + 2 * (y * w + x))[0]
            a = 0 if flags and p == key else 255
            row += bytes([((p >> 10) & 31) * 255 // 31, ((p >> 5) & 31) * 255 // 31, (p & 31) * 255 // 31, a])
        rows.append(bytes(row))
    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(b''.join(rows))) + chunk(b'IEND', b''))
    return w, h


def ids_of(xml):
    text = open(xml, encoding='utf-8').read()
    out = []
    for m in re.finditer(r'(\w*Surf)\s*=\s*"(\d+)', text):
        out.append(int(m.group(2)))
    return out


def main():
    os.makedirs(os.path.join(OUT, 'png'), exist_ok=True)
    ids = set()
    for x in sys.argv[1:]:
        ids |= set(ids_of(x))
    # 執行期才定的 surf：選角職業卡 6430–6489、上下頁 6411–6414、Character_List 版 7045–7091、8365–8372、建角背景 7156–7171、7265/7266
    ids |= set(range(6400, 6500)) | set(range(7045, 7092)) | set(range(8365, 8373)) | set(range(7156, 7172)) | {7265, 7266}
    rows = ['id\t' + '\t'.join('%s_file\t%s_size\t%s_sha12' % (c, c, c) for c, _ in CLIENTS) + '\tmain==kor25']
    for i in sorted(ids):
        cells = [str(i)]
        shas = []
        for tag, root in CLIENTS:
            f, data = find(root, str(i))
            if data is None:
                cells += ['-', '-', '-']
                shas.append(None)
                continue
            try:
                w, h = png(os.path.join(OUT, 'png', '%s_%d.png' % (tag, i)), data)
                size = '%dx%d' % (w, h)
            except Exception as ex:  # noqa: BLE001
                size = 'bad:' + str(ex)
            sha = hashlib.sha256(data).hexdigest()
            shas.append(sha)
            cells += [f, size, sha[:12]]
        cells.append('Y' if shas[0] and shas[0] == shas[1] else 'N')
        rows.append('\t'.join(cells))
    with open(os.path.join(OUT, 'surf-compare.tsv'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(rows) + '\n')
    print('\n'.join(rows))


if __name__ == '__main__':
    main()
