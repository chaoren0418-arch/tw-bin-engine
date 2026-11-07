# Issue #417 探測輸出

`docs/reference/rea-scripts/` 各腳本的原始輸出，判讀見 `docs/reference/rea-inventory.md`。

## xver.txt

```text
TW1608262200 16810496 e12949cf8912c5d6 imm1951 13 imm1812 10 paired 2 ['0x47d626', '0x47d6f1'] strings {'list.spr': 7, 'loadSprites': 1, 'LoadSprite Start': 1}
TW13081901.unpacked 11624448 653da514f750d471 imm1951 5 imm1812 6 paired 1 ['0xa4bf0'] strings {'list.spr': 3, 'loadSprites': 0, 'LoadSprite Start': 0}
LiTo.unpacked-oep-current 7094272 4271d92fb2b9a2fa imm1951 4 imm1812 14 paired 2 ['0x3e85c', '0xe9701'] strings {'list.spr': 2, 'loadSprites': 0, 'LoadSprite Start': 0}
Lin.bin(VMP) 11365984 fc2e1f57cc9f2e30 imm1951 1 imm1812 1 paired 0 [] strings {'list.spr': 5, 'loadSprites': 0, 'LoadSprite Start': 0}
LinR.bin 235703808 7467619438c074ab imm1951 16 imm1812 15 paired 3 ['0xaa82fb', '0xb52bab', '0xb52c16'] strings {'list.spr': 9, 'loadSprites': 1, 'LoadSprite Start': 1}
```

## tw13-parser.txt

```text
parser candidate 0x4a34f0 {'cmp=': 2, 'cmp>': 2, 'cmp<': 2, 'cmp[': 2, 'cmp^': 2, 'cmp]': 1, 'cmp{': 1, 'cmp}': 1, 'cmp!': 1}
 first {'cmp=': '0x4a3ef2', 'cmp>': '0x4a485b', 'cmp<': '0x4a4e87', 'cmp[': '0x4a4f74', 'cmp^': '0x4a5061', 'cmp]': '0x4a51a4', 'cmp{': '0x4a524d', 'cmp}': '0x4a52f2', 'cmp!': '0x4a5392'}
 set +0x14 [('0x4a4e1b', 'byte ptr [eax + edx + 0x14], 0'), ('0x4a4f64', 'byte ptr [eax + ecx + 0x14], 0'), ('0x4a5051', 'byte ptr [eax + ecx + 0x14], 1'), ('0x4a5177', 'byte ptr [eax + ecx + 0x14], 2')]
 or flags [('0x4a5237', 'ecx, 4'), ('0x4a52dc', 'ecx, 8'), ('0x4a5381', 'ecx, 0x10'), ('0x4a53c0', 'ecx, 1'), ('0x4a5408', 'edx, 2')]
 xor imm [('0x4a4bee', 'esi, 0x1951'), ('0x4a4cb5', 'ebx, 0x2b'), ('0x4a4d5d', 'esi, 0x1812')]
```

## consumers.txt

```text
swept to 0x114a7ff hits 18
with imul 0x24 nearby 9
  0081C90C movzx eax, byte ptr [eax + edx + 0x16]
  008AC256 movzx eax, byte ptr [eax + edx + 0x16]
  008AC3BF movzx eax, byte ptr [eax + edx + 0x16]
  008ACC68 movzx edx, byte ptr [eax + ecx + 0x16]
  00A3FBB8 movsx eax, byte ptr [eax + edx + 0x14]
  00A3FC2E movsx eax, byte ptr [eax + edx + 0x14]
  00A47A56 movzx edx, byte ptr [eax + ecx + 0x16]
  00A4B6D0 movzx eax, byte ptr [eax + edx + 0x16]
  00A4BC45 movzx eax, byte ptr [edx + ecx + 0x16]
== TW13081901
 parser 4E42D0 {'or+16': 1, 'cmp{': 1}
    or+16 004E4313 or ecx, 1
    cmp{ 004E52D6 cmp ecx, 0x7b
 selector 5A9EE0 {'abs+14': 14, 'cmp5': 2, 'abs+c': 2, 'cmp2': 3}
    abs+14 005A9F25 movsx ecx, byte ptr [eax + 0x14]
    cmp5 005AA648 cmp dword ptr [ebp - 0x28], 5
    abs+c 005AA777 movzx eax, word ptr [eax + edx + 0xc]
    cmp2 005AA933 cmp eax, 2
```

## final-probe.txt

```text
== frame byte consumers (+0x14 mode, +0x16 flags) in A00000-A60000, E00000-F20000
decoded to 0xa3921f
  00A0C05F  0fb64514             movzx eax, byte ptr [ebp + 0x14]   <<add edx, 0x80; cmp dword ptr [ebp - 0x24], edx; jge 0xa0c099>>  >>push eax; mov ecx, dword ptr [ebp + 8]>>
== switch 1 landing for byte values
  op   8 (0x08) -> 00947DF4 calls ['0x4f9b90', '0x8574f0', '0x103cea6']  shared_with 139 values
  op  11 (0x0B) -> 00947DF4 calls ['0x4f9b90', '0x8574f0', '0x103cea6']  shared_with 139 values
  op  87 (0x57) -> 00947DF4 calls ['0x4f9b90', '0x8574f0', '0x103cea6']  shared_with 139 values
  op 112 (0x70) -> 00947668 calls ['0x94b4f0']  shared_with 1 values
  op 158 (0x9E) -> 009479D0 calls ['0x942840']  shared_with 1 values
  op 206 (0xCE) -> 009476BD calls ['0x933000']  shared_with 1 values
  op 226 (0xE2) -> 009477AB calls ['0x92cb70']  shared_with 1 values
default target 0x947a69 n cases 115
== who calls factory A4E5D0 / self-object A51920 from switch region
  A4E5D0 direct calls 3: ['0x7c3eba', '0xdf55f2', '0xf0eb0e']
  A51920 direct calls 6: ['0xa4c043', '0xa4e846', '0xa50d83', '0xa50e23', '0xa5104b', '0xa51171']
== TW13081901 parser cross-check
  header ['\ufeffaddress_hex', 'length', 'type', 'xref_count', 'text', 'xrefs', 'functions']
   0x008C9228,41,0,1,exceed the sprite number(%d) in list.spr,['0x0040B464'],['0x0040B3A0']
   0x008CFFA8,9,0,1,list.spr,['0x004A3532'],['0x004A34F0']
   0x008D29EC,9,0,1,list.spr,['0x004E4B35'],['0x004E42D0']
   0x008DBF60,30,0,1,Sprite file %d-%d.spr exceed!,['0x005A9C0D'],['0x005A9BE0']
   0x008DBFB8,36,0,1,Sprite file %d-%d.spr doesn't exist,['0x005AA14C'],['0x005A9EE0']
   0x008DBFDC,36,0,1,Sprite file %d-%d.spr doesn't exist,['0x005AA421'],['0x005A9EE0']
   0x008DC000,36,0,1,Sprite file %d-%d.spr doesn't exist,['0x005AA557'],['0x005A9EE0']
```

## b04-tokens.txt

```text
head '30000 0 41211\n#0\t312=3225\tprince\n\t0.walk(1 4,0.0:4<479 0.1:4 0.2:4<478 0.3:4)\n\t1.attack(1 7,0.0:0 16.0:3 16.1:3 16.2:3 16.3:5[1539 16.4:2! 16.5:4)\n\t2.damage(1 3'
blocks 30000 [(0, 14), (1, 3640), (2, 7452)]
6016 block: '#6016 1 gludio object one\n\t3.animation(0 12,0.11:5 0.0:5<3332<1340<1340 0.1:5 0.2:5 0.3:5 0.4:5 0.5:5 0.6:5 0.7:5 0.8:5 0.9:5 0.10:5)\n\t102.type(0)\n'
effect ids 521 no block 0 ids with tokens 184
ids per symbol {'^': 111, '<': 63, '[': 10} records per symbol {'^': 8003, '<': 1751, '[': 61}
sample [(1909, {'^': 1}), (1910, {'^': 1}), (24657, {'^': 1}), (1908, {'^': 1}), (4814, {'<': 1}), (1872, {'^': 1}), (1648, {'^': 1}), (1863, {'^': 1}), (1870, {'^': 1}), (4775, {'^': 1}), (6818, {'<': 1}), (6817, {'<': 1})]
== LiTo window
0x3e85c {'xor 0x1951': 1, 'xor 0x2b': 1, '>': 2, 'xor 0x1812': 1, '<': 2, '[': 2, '^': 2, ']': 1, '!': 1}
0xe9701 {'xor 0x1812': 6, '>': 2, 'xor 0x1951': 1}
```

## actor612-uses.txt

```text
functions 14 uses 17
006A1880
    006A1985  c7806402000000000000 mov dword ptr [eax + 0x264], 0
0085E090
    0085E544  8b9064020000         mov edx, dword ptr [eax + 0x264]
0085E600
    0085E99F  8b8a64020000         mov ecx, dword ptr [edx + 0x264]
0085EA90
    0085EE0E  8b8a64020000         mov ecx, dword ptr [edx + 0x264]
0085EE80
    0085F19C  8b9064020000         mov edx, dword ptr [eax + 0x264]
00885EC0
    0088603C  899164020000         mov dword ptr [ecx + 0x264], edx
00886EB0
    0088700E  3b8164020000         cmp eax, dword ptr [ecx + 0x264]
00887720
    00887773  8b9164020000         mov edx, dword ptr [ecx + 0x264]
    0088780F  3b8264020000         cmp eax, dword ptr [edx + 0x264]
    0088781D  8b9164020000         mov edx, dword ptr [ecx + 0x264]
00888910
    00888AA5  3b8164020000         cmp eax, dword ptr [ecx + 0x264]
00919170
    0091949A  c7816402000000000000 mov dword ptr [ecx + 0x264], 0
00919F30
    0091AA73  898164020000         mov dword ptr [ecx + 0x264], eax
0091D080
    0091D221  83ba6402000000       cmp dword ptr [edx + 0x264], 0
    0091D238  8b8a64020000         mov ecx, dword ptr [edx + 0x264]
00A3A530
    00A3A9BA  c78064020000ffffffff mov dword ptr [eax + 0x264], 0xffffffff
00F0E9B0
    00F0EB5B  898264020000         mov dword ptr [edx + 0x264], eax
```
