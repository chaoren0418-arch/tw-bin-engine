"""Read-only, no-reparse inventory. Writes only this documentation directory."""
import os, json, hashlib, datetime, collections, gzip
from pathlib import Path
ROOT=Path(r'D:\lineage開發\TW1608262200研究')
OUT=Path(__file__).parent
PATHS=[Path(r'D:\P10Lab-TW13081901')]+[Path(f'D:\\P{i}Lab-ClientEngine') for i in range(12,25)]+[Path(r'D:\P24Lab-6016-Export-r1'),Path(r'D:\TW1608262200_research')]
def walk(root):
    stack=[root]
    while stack:
        d=stack.pop()
        try:
            with os.scandir(d) as it:
                for e in it:
                    try:
                        s=e.stat(follow_symlinks=False)
                        if getattr(s,'st_file_attributes',0)&0x400:
                            yield 'link',Path(e.path),s
                        elif e.is_dir(follow_symlinks=False): stack.append(Path(e.path))
                        else: yield 'file',Path(e.path),s
                    except OSError as ex: yield 'error',Path(e.path),str(ex)
        except OSError as ex: yield 'error',Path(d),str(ex)
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(4*1024*1024),b''): h.update(b)
    return h.hexdigest()
def main():
    rows=[]; summaries=[]; links=[]; errors=[]; agents=[]
    for root in PATHS+[ROOT]:
        c=collections.Counter(); n=0; size=0; latest=0
        for kind,p,s in walk(root):
            rel=p.relative_to(root).as_posix()
            if root==ROOT and rel.startswith('docs/project-consolidation/'): continue
            if kind=='link': links.append({'root':str(root),'path':rel,'target':os.readlink(p)});continue
            if kind=='error':errors.append({'path':str(p),'error':s});continue
            n+=1;size+=s.st_size;latest=max(latest,s.st_mtime)
            c[rel.split('/')[0] if '/' in rel else '(root files)']+=s.st_size
            if p.name.upper()=='AGENTS.MD': agents.append(str(p))
            rows.append({'root':str(root),'path':rel,'bytes':s.st_size,'mtime_ns':s.st_mtime_ns})
        summary={'root':str(root),'files':n,'logical_bytes':size,'latest_file_utc':datetime.datetime.fromtimestamp(latest,datetime.timezone.utc).isoformat(),'top_level_bytes':dict(c.most_common())}
        summaries.append(summary);print(json.dumps({k:v for k,v in summary.items() if k!='top_level_bytes'},ensure_ascii=False),flush=True)
    with gzip.open(OUT/'file-metadata.jsonl.gz','wt',encoding='utf-8') as f:
        for r in rows:f.write(json.dumps(r,ensure_ascii=False)+'\n')
    result={'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'method':'os.scandir, skip FILE_ATTRIBUTE_REPARSE_POINT; logical sizes, not allocated disk usage; no database queries','roots':summaries,'links':links,'errors':errors,'agents':agents}
    (OUT/'inventory.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('DONE',len(rows),'files',len(links),'links',len(errors),'errors',flush=True)
if __name__=='__main__':main()
