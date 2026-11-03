"""Hash static Lab content and audit textual path dependencies without executing it."""
import json, re, collections, datetime
from pathlib import Path
from inventory import ROOT,OUT,PATHS,walk,sha
P24=next(ROOT.glob('P24-*'))
DELIVERY=P24/'reference/development/release-integration-r12/delivery'
EXCLUDED={'db-data','db-logs','db-temp','tmp','logs','loginfo','runs','runtime','home','.dotnet','.nuget','__pycache__'}
TEXT_EXT={'.py','.ps1','.cmd','.bat','.cs','.csproj','.json','.md','.ini','.xml','.properties','.config','.js','.html','.txt'}
def excluded(rel):
    return any(p.lower() in EXCLUDED for p in rel.parts) or any('private' in p.lower() or 'credential' in p.lower() for p in rel.parts)
def main():
    hashes=[];skip=[];errors=[];groups=collections.defaultdict(list)
    for root in PATHS:
        count=0
        for kind,p,s in walk(root):
            if kind!='file':continue
            rel=p.relative_to(root)
            if excluded(rel):skip.append({'root':str(root),'path':rel.as_posix(),'bytes':s.st_size,'reason':'mutable-or-private'});continue
            try:
                digest=sha(p); after=p.stat()
                stable=after.st_mtime_ns==s.st_mtime_ns and after.st_size==s.st_size
                row={'root':str(root),'path':rel.as_posix(),'bytes':s.st_size,'sha256':digest,'stable_during_hash':stable}
                hashes.append(row)
                if stable:groups[(s.st_size,digest)].append(row)
                count+=1
            except OSError as ex:errors.append({'path':str(p),'error':str(ex)})
        print('HASH',str(root),count,flush=True)
    dup=[{'bytes_each':k[0],'sha256':k[1],'files':[str(Path(r['root'])/r['path']) for r in v]} for k,v in groups.items() if len({r['root'] for r in v})>1]
    unique=[r for r in hashes if r['stable_during_hash'] and len({x['root'] for x in groups[(r['bytes'],r['sha256'])]})==1]
    stats=[]
    for root in PATHS:
        rs=[r for r in hashes if r['root']==str(root)];us=[r for r in unique if r['root']==str(root)];ss=[r for r in skip if r['root']==str(root)]
        stats.append({'root':str(root),'hashed_files':len(rs),'hashed_bytes':sum(r['bytes'] for r in rs),'unique_within_hashed_16_roots':len(us),'unique_bytes':sum(r['bytes'] for r in us),'excluded_files':len(ss),'excluded_bytes':sum(r['bytes'] for r in ss)})
    (OUT/'static-hashes.jsonl').write_text(''.join(json.dumps(r,ensure_ascii=False)+'\n' for r in hashes),encoding='utf-8')
    (OUT/'unique-static-files.jsonl').write_text(''.join(json.dumps(r,ensure_ascii=False)+'\n' for r in unique),encoding='utf-8')
    (OUT/'duplicate-groups.json').write_text(json.dumps(dup,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'hash-summary.json').write_text(json.dumps({'scope':'All regular static files in specified 16 roots; database/runtime/private excluded; unique means only among hashed roots, not unique in all disks','roots':stats,'excluded':skip,'errors':errors},ensure_ascii=False,indent=2),encoding='utf-8')
    print('DONE HASH',len(hashes),len(dup),flush=True)
if __name__=='__main__':main()
