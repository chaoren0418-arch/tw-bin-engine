"""Full file SHA before a named migration; never follow reparse points; no content output."""
import sys,json,os,hashlib,datetime,gzip
from pathlib import Path
OUT=Path(__file__).parent
ROOT=OUT.parents[2]
def digest(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def walk(root):
    stack=[root]
    while stack:
        d=stack.pop()
        for e in os.scandir(d):
            p=Path(e.path);s=p.stat(follow_symlinks=False)
            if getattr(s,'st_file_attributes',0)&0x400:yield 'link',p,s
            elif e.is_dir(follow_symlinks=False):stack.append(p)
            else:yield 'file',p,s
def capture(name,root):
    dest=OUT/(name+'.jsonl.gz');assert not dest.exists()
    count=size=0;links=[]
    with gzip.open(dest,'wt',encoding='utf-8') as out:
        for kind,p,s in walk(root):
            rel=p.relative_to(root).as_posix()
            if kind=='link':links.append({'path':rel,'target':os.readlink(p)});continue
            volatile=name=='before-r28' and rel.split('/')[0] in {'runs','runtime','.nuget','.dotnet','tmp'}
            h=None if volatile else digest(p);after=p.stat();assert (s.st_size,s.st_mtime_ns)==(after.st_size,after.st_mtime_ns),str(p)
            out.write(json.dumps({'path':rel,'bytes':s.st_size,'sha256':h,'hash_scope':'same-volume identity only; mutable evidence not deduped' if volatile else 'full-sha256','mtime_ns':s.st_mtime_ns,'file_id':s.st_ino},ensure_ascii=False)+'\n');count+=1;size+=s.st_size
            if count%2000==0:print(name,count,'files',flush=True)
    receipt={'root':str(root),'files':count,'bytes':size,'links':links,'snapshot':str(dest),'snapshot_sha256':digest(dest),'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
    (OUT/(name+'.json')).write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(receipt,ensure_ascii=False),flush=True)
if __name__=='__main__':
    if sys.argv[1]=='r28':capture('before-r28',ROOT/'P24-原2x2地圖快取與跨區載入/reference/development/release-integration-r12/delivery')
    elif sys.argv[1]=='external':
        paths=[Path(r'D:\P10Lab-TW13081901')]+[Path(f'D:\\P{i}Lab-ClientEngine') for i in range(12,25)]+[Path(r'D:\P24Lab-6016-Export-r1'),Path(r'D:\TW1608262200_research')]
        for p in paths:capture('before-'+p.name,p)
