# Only derived route coordinates/hashes are emitted; no source or map bytes.
from pathlib import Path
import argparse,csv,json,hashlib,heapq
ap=argparse.ArgumentParser(description="Read-only static meat route check; never connects to DB or copies game assets")
ap.add_argument("--server-root",type=Path,required=True)
ap.add_argument("--assets-root",type=Path,required=True)
ap.add_argument("--output",type=Path,required=True)
ap.add_argument("--start",type=int,nargs=2,default=[32600,32940],metavar=("X","Y"))
args=ap.parse_args()
server=args.server_root
assets=args.assets_root
sql=server/'db/380_db.sql'
def records(table):
    prefix='INSERT INTO `'+table+'` VALUES ('
    for line in sql.read_text('utf-8').splitlines():
        if line.startswith(prefix):
            yield next(csv.reader([line[len(prefix):-2]],quotechar="'",skipinitialspace=True))
spawn=list(records('spawnlist_npc'))
npc=next(r for r in spawn if r[3]=='70014')
assert (int(npc[4]),int(npc[5]),int(npc[10]))==(32645,32951,0)
shop=next(r for r in records('shop') if r[0]=='70014' and r[1]=='40056')
assert int(shop[3])>0 and max(1,int(shop[4]))==1
blocked={(int(r[4]),int(r[5])) for r in spawn if r[10]=='0' and int(r[2])>0}
for r in records('spawnlist_door'):
    if r[5]=='0':
        # Avoid the complete closed-door span regardless of current door state.
        x,y=int(r[3]),int(r[4]); start,end=int(r[7]),int(r[8])
        for a in range(start,end+1):blocked.add((a,y) if r[6]=='0' else (x,a))
manifest=assets/'maps/manifest.json'; meta=next(m for m in json.loads(manifest.read_text('utf-8'))['maps'] if m['mapId']==0)
clientfile=assets/'maps'/meta['terrain']['path']; client=clientfile.read_bytes(); assert hashlib.sha256(client).hexdigest()==meta['terrain']['sha256']
serverfile=server/'maps/0.txt'; rows=[[int(v)&255 for v in l.split(',') if v.strip()] for l in serverfile.read_text('utf-8-sig').splitlines() if l.strip() and not l.startswith('#')]
assert len(rows)==512 and all(len(r)==512 for r in rows)
minx,miny=meta['minX'],meta['minY']
def tile(x,y,kind):
    if not(minx<=x<minx+512 and miny<=y<miny+512):return 0
    return rows[y-miny][x-minx] if kind==0 else client[(y-miny)*512+x-minx]
dirs={0:(0,-1),2:(1,0),4:(0,1),6:(-1,0)}
def ok(p,h):
    x,y=p; dx,dy=dirs[h]; q=(x+dx,y+dy)
    if q in blocked:return False
    for k in (0,1):
        a,v=tile(x,y,k),tile(*q,k)
        if not v or v&128 or not v&3:return False
        if not {0:a&2,2:a&1,4:v&2,6:v&1}[h]:return False
    return True
start=tuple(args.start);goal=(32644,32951)
queue=[(0,*start,-1)];dist={(*start,-1):0};prev={};end=None
while queue:
    cost,x,y,last=heapq.heappop(queue); state=(x,y,last)
    if dist[state]!=cost:continue
    if (x,y)==goal:end=state;break
    for h,(dx,dy) in dirs.items():
        if not ok((x,y),h):continue
        nxt=(x+dx,y+dy,h); nc=cost+10+(0 if last in(-1,h) else 4)
        if nc<dist.get(nxt,10**9):dist[nxt]=nc;prev[nxt]=state;heapq.heappush(queue,(nc,*nxt))
assert end,'no legal static route'
steps=[]
while end[:2]!=start:steps.append(end);end=prev[end]
steps.reverse();legs=[]
for x,y,h in steps:
    if legs and legs[-1]['heading']==h:legs[-1]['count']+=1;legs[-1]['end']=[x,y]
    else:legs.append(dict(heading=h,count=1,end=[x,y]))
files=[sql,serverfile,manifest,clientfile]
out=dict(status='static-pass',npc=70014,map=0,start=start,goal=goal,steps=len(steps),legs=legs,basePrice=int(shop[3]),packCount=max(1,int(shop[4])),sources=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in files])
args.output.write_text(json.dumps(out,ensure_ascii=False,indent=2),'utf-8');print(json.dumps(out,ensure_ascii=False,indent=2))
