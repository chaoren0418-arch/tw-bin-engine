"""Issue #102 唯讀追蹤內容盤點；不是發布核准器。只寫指定 scratch 輸出。"""
import argparse,base64,collections,gzip,hashlib,io,json,pathlib,re,stat,subprocess,zipfile,xml.etree.ElementTree as ET
MAX=64*1024*1024
META_KEYS=set('path relativePath relative_path sha256 SHA256 sha hash bytes size length role source sourcePath sourceSha256 files schema version utc root snapshot snapshot_sha256 links total totalBytes count name kind status missing extra changed unchanged expected actual algorithm sourceCount excludedFiles excludedDirectories revision candidateId audioDllSha256 productDllSha256 launcherDllSha256 productManifestSha256 dllSha256'.split())
CODE_EXT={'.cs','.py','.ps1','.js','.cjs','.css','.html','.cmd','.csproj','.config','.yml','.mdc'}
KNOWN_DATA={'engine/source/Resources/pickup-own-base15-gfx0-weapon0.json':'D08：SPR 派生 RGB555 frames.blocks.pixels；PickupAssets.ReadBody 消費', 'engine/source/Resources/pickup-own-base15-profile-extension.json':'D08：SPR 派生 groups.frames.blocks.pixels；PickupAssets.ReadBody 消費', 'engine/_integration/check-template/fixtures/6016.json':'D08：6016 SPR 派生 frames.blocks.pixels 測試素材', 'engine/source/OriginalExpTable.cs':'D04：程式註解及 generate_exp_table.py 證明 SQL exp 表轉為 Thresholds', 'engine/source/Resources/ground-catalog.json':'D05：rows 保存原服物品欄位與 SPR bounds；混合 pins 不使資料變成純 metadata','engine/action-metadata-vectors.json':'D06：動作 metadata 時序／序列測試向量','engine/haste-metadata-vectors.json':'D06：動作 metadata 時序／序列測試向量','engine/_integration/dev/combat-sfx-tests/expected-events.json':'D06：原始動作 sound events 預期向量'}
MANUAL_META={'AGENTS.md','CLAUDE.md','PLAN.md','TASKS.md','README.md','.gitignore','.gitattributes','engine/global.json','engine/launcher/P24.Launcher.deps.json','engine/launcher/P24.Launcher.runtimeconfig.json','engine/audio/Resources/combat-sfx-pins.json','docs/design/portable-delivery.md','docs/REPORT.md'}
def shape(v):
 if isinstance(v,dict):return 'keys='+','.join(list(v)[:6])
 if isinstance(v,list):return 'list('+str(len(v))+');'+(shape(v[0]) if v else 'empty')
 return type(v).__name__
def safe_meta(v):
 if isinstance(v,dict):return bool(v) and set(v)<=META_KEYS and all(safe_meta(x) for x in v.values())
 if isinstance(v,list):return all(safe_meta(x) for x in v)
 if isinstance(v,str):return len(v)<=1024 and not re.search(r'(?i)data:|base64|INSERT INTO|SELECT .* FROM',v)
 return v is None or isinstance(v,(int,float,bool))
def signals(v,loc='$',found=None):
 if found is None:found=[]
 if isinstance(v,dict):
  for k,x in v.items():
   name=k.lower();at=loc+'.'+k
   if name in {'password','passwd','secret','token','connectionstring','account'}: found.append(('sensitive',at))
   if name in {'pixels','rgba','rgb555','pixeldata','pixelbytes'} and isinstance(x,list) and x:found.append(('pixels',at))
   if name in {'record_hex','rawhex','name_field_hex','payloadhex'} and isinstance(x,str) and re.fullmatch('[0-9a-fA-F]+',x or ' '):found.append(('hex',at))
   signals(x,at,found)
 elif isinstance(v,list):
  for i,x in enumerate(v):signals(x,loc+'[]',found)
 elif isinstance(v,str):
  if v.startswith('data:') and ';base64,' in v:found.append(('encoded',loc))
  elif len(v)>=128 and len(v)%4==0 and re.fullmatch('[A-Za-z0-9+/]+={0,2}',v):
   try:
    b=base64.b64decode(v,validate=True)
    if b.startswith((b'\x89PNG',b'RIFF',b'GIF8',b'PK\x03\x04',b'\x1f\x8b')):found.append(('encoded',loc))
    else:found.append(('opaque',loc))
   except ValueError:pass
 return found

def classify(p,b,depth=0):
 ext=pathlib.PurePosixPath(p).suffix.lower()
 if len(b)>MAX:return 'U','U01：超過解碼上限 unknown'
 if b.startswith(b'\x89PNG\r\n\x1a\n'):
  return ('D','D01：PNG magic；TBT／SPR 派生，詳逐圖表') if p.startswith('engine/web/') else ('D','D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收')
 if b.startswith(b'RIFF') and b[8:12]==b'WAVE':return 'D','D03：RIFF/WAVE 原音訊；delta assets 歷史整合'
 if p in KNOWN_DATA:return 'D',KNOWN_DATA[p]
 if b.startswith(b'\x1f\x8b'):
  try:
   with gzip.GzipFile(fileobj=io.BytesIO(b)) as f: raw=f.read(MAX+1)
   if len(raw)>MAX:raise ValueError('limit')
   lines=[json.loads(l) for l in raw.decode('utf-8-sig').splitlines() if l.strip()]
   return ('M','M02：gzip 解壓全列均為 path/size/hash 清冊；'+str(len(lines))+' 列') if all(safe_meta(x) for x in lines) else ('U','U02：gzip 已解壓，非白名單 JSONL schema；unknown')
  except Exception as e:return 'U','U03：gzip 無法完整驗證 '+type(e).__name__+'；unknown'
 if b.startswith(b'PK\x03\x04'):
  try:
   with zipfile.ZipFile(io.BytesIO(b)) as z:
    if sum(i.file_size for i in z.infolist())>MAX:raise ValueError('limit')
    cats=[classify(i.filename,z.read(i),depth+1)[0] for i in z.infolist() if not i.is_dir()]
    return ('D','D07：ZIP member 含遊戲資料') if 'D' in cats else ('U','U04：ZIP '+str(len(cats))+' members 已讀；文件語義未全數核准 unknown')
  except Exception as e:return 'U','U04：ZIP '+type(e).__name__+'；unknown'
 try:t=b.decode('utf-8-sig')
 except UnicodeError:return 'U','U05：未識別 binary/encoding；unknown'
 if ext in {'.json','.jsonl'} or t.lstrip().startswith(('{','[')):
  try:v=json.loads(t) if ext!='.jsonl' else [json.loads(l) for l in t.splitlines() if l.strip()]
  except Exception as e:return 'U','U06：JSON parse '+type(e).__name__+'；unknown'
  sig=signals(v)
  if any(k in {'pixels','encoded','hex'} for k,_ in sig):
   at=next(at for k,at in sig if k in {'pixels','encoded','hex'});return 'U','U14：可重構素材／封包欄位 '+at[:100]+'；真實或合成來源 unknown'
  if any(k=='sensitive' for k,_ in sig):return 'U','U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown'
  if any(k=='opaque' for k,_ in sig):return 'U','U08：opaque Base64 候選；來源 unknown'
  if p in MANUAL_META or safe_meta(v):return 'M','M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins'
  return 'U','U09：JSON 證據缺逐欄來源裁定；unknown；'+shape(v)[:130]
 if p in MANUAL_META:return 'M','M03：已核讀工程規則／設計／索引，不含素材本文'
 if ext in CODE_EXT:
  if re.search(r'(?:\d+(?:UL|ul|u|L)?\s*,\s*){40}',t):return 'U','U10：程式長數值常數；須追溯資料來源 unknown'
  if re.search(r'[A-Za-z0-9+/]{256,}={0,2}',t):return 'U','U10：程式 opaque 長字串；unknown'
  return 'C','C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證'
 return 'U','U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown'

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--tree',required=True);ap.add_argument('--out',required=True);args=ap.parse_args()
 root=pathlib.Path(args.tree).resolve();out=pathlib.Path(args.out).resolve()
 if 'scratch' not in out.parts or out==root or root in out.parents:raise SystemExit('output must be separate scratch directory')
 out.mkdir(parents=True,exist_ok=True)
 entries=subprocess.check_output(['git','ls-files','--stage','-z'],cwd=root).split(b'\0');rows=[];projects=[];containers=[]
 proc=subprocess.Popen(['git','cat-file','--batch'],cwd=root,stdin=subprocess.PIPE,stdout=subprocess.PIPE)
 for e in entries:
  if not e:continue
  meta,pb=e.split(b'\t',1);mode,oid,stage=meta.decode().split();p=pb.decode()
  if stage!='0':raise RuntimeError('unmerged index')
  proc.stdin.write(oid.encode()+b'\n');proc.stdin.flush();header=proc.stdout.readline().split();n=int(header[-1]);b=proc.stdout.read(n);assert proc.stdout.read(1)==b'\n'
  category,reason=classify(p,b)
  if mode not in {'100644','100755'}:category,reason='U','U12：非普通檔 Git mode '+mode+'；unknown'
  # Check ancestors without following junctions; never enumerate untracked trees.
  f=root/p;cur=f;link=False
  while cur!=root:
   s=cur.lstat()
   if stat.S_ISLNK(s.st_mode) or getattr(s,'st_file_attributes',0)&0x400:link=True;break
   cur=cur.parent
  if link:category,reason='U','U13：checkout 路徑有 reparse/symlink；未讀目標 unknown';worksha='not-read'
  else:worksha=hashlib.sha256(f.read_bytes()).hexdigest()
  rows.append(dict(path=p,mode=mode,oid=oid,bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),worktreeSha256=worksha,category=category,reason=reason))
  if p.endswith('.csproj'):
   x=ET.fromstring(b);projects.append(dict(path=p,embedded=[dict(i.attrib) for i in x.iter('EmbeddedResource')],references=[dict(i.attrib) for i in x.iter('ProjectReference')]))
 proc.stdin.close();proc.wait();assert proc.returncode==0
 (out/'inventory.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 (out/'projects.json').write_text(json.dumps(projects,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 lines=['## 附錄：全部追蹤路徑逐項帳冊','', 'SHA-256 為 Git blob 原始 bytes（避免 CRLF checkout 差異）；C/M/D/U 定義見正文。每列只分類該路徑，不遞迴允許所在目錄。','', '| 路徑 | bytes | SHA-256 | 分類與依據 |','|---|---:|---|---|']
 for x in rows:lines.append('| `'+x['path']+'` | '+str(x['bytes'])+' | `'+x['sha256']+'` | '+x['category']+' — '+x['reason'].replace('|','／')+' |')
 (out/'appendix.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
 summary=dict(commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root).decode().strip(),files=len(rows),categories=dict(collections.Counter(x['category'] for x in rows)),projects=len(projects),embedded=sum(len(x['embedded']) for x in projects),modeCounts=dict(collections.Counter(x['mode'] for x in rows)),checkoutHashDifferences=sum(x['sha256']!=x['worktreeSha256'] for x in rows),inventorySha256=hashlib.sha256((out/'inventory.json').read_bytes()).hexdigest())
 (out/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf8');print(json.dumps(summary,ensure_ascii=False))
if __name__=='__main__':main()
