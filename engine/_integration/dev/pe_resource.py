"""Read CLR ManifestResources directly from PE bytes; never load/execute the DLL.

Portable inspection uses metadata tables row sizes through ManifestResource (40).
`manifest_resources(data)` lists every embedded resource (T472 portable zip review);
`verify_resource(path)` keeps the original single-resource pin check.
"""
import hashlib,struct
RESOURCE_SHA='43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90'
LOGICAL='P06.Resources.PickupOwnBase15Gfx0Weapon0.json'
def require(ok,message):
 if not ok:raise ValueError(message)
def manifest_resources(data):
 """Return None for a native PE (no CLI header), else [dict(logicalName, embedded, payload)]."""
 def u16(p):return struct.unpack_from('<H',data,p)[0]
 def u32(p):return struct.unpack_from('<I',data,p)[0]
 require(data[:2]==b'MZ','not PE');pe=u32(60);require(data[pe:pe+4]==b'PE\0\0','PE signature')
 n=u16(pe+6);opt=pe+24;magic=u16(opt);require(magic in [0x10b,0x20b],'PE optional header')
 sections=opt+u16(pe+20)
 def rva(address):
  for i in range(n):
   p=sections+40*i;va=u32(p+12);raw=u32(p+16)
   if va<=address<va+raw:return u32(p+20)+address-va
  raise ValueError('RVA outside raw section')
 dd=opt+(96 if magic==0x10b else 112);count=u32(dd-4)
 if count<15 or u32(dd+14*8)==0:return None
 cli=rva(u32(dd+14*8));metadata=rva(u32(cli+8));resource_size=u32(cli+28)
 resources=rva(u32(cli+24)) if resource_size else 0
 require(data[metadata:metadata+4]==b'BSJB','metadata signature');p=(metadata+16+u32(metadata+12)+3)&~3;streams=u16(p+2);p+=4;headers={}
 for _ in range(streams):
  offset=u32(p);size=u32(p+4);end=data.index(0,p+8);name=data[p+8:end].decode('ascii');headers[name]=(metadata+offset,size);p=(end+4)&~3
 table=headers.get('#~',headers.get('#-'));require(table is not None,'metadata tables');t=table[0];heap=data[t+6];valid=struct.unpack_from('<Q',data,t+8)[0];p=t+24;rows={}
 for i in range(64):
  if valid>>i&1:rows[i]=u32(p);p+=4
 def idx(i):return 4 if rows.get(i,0)>=65536 else 2
 def coded(bits,ids):return 4 if max([rows.get(i,0) for i in ids]+[0]) >= (1<<(16-bits)) else 2
 s=4 if heap&1 else 2;b=4 if heap&4 else 2;g=4 if heap&2 else 2
 td=coded(2,[2,1,27]);resolution=coded(2,[0,26,35,1]);member=coded(3,[2,1,26,6,27]);method=coded(1,[6,10]);hasconst=coded(2,[4,8,23]);custom=coded(5,[6,4,1,2,8,9,10,0,14,23,20,17,26,27,32,35,38,39,40,42,44,43]);catype=coded(3,[6,10]);impl=coded(2,[38,35,39])
 sizes={0:2+s+3*g,1:resolution+2*s,2:4+2*s+td+idx(4)+idx(6),3:idx(4),4:2+s+b,5:idx(6),6:8+s+b+idx(8),7:idx(8),8:4+s,9:idx(2)+td,10:member+s+b,11:2+hasconst+b,12:custom+catype+b,13:coded(1,[4,8])+b,14:2+coded(2,[2,6,32])+b,15:6+idx(2),16:4+idx(4),17:b,18:idx(2)+idx(20),19:idx(20),20:2+s+td,21:idx(2)+idx(23),22:idx(23),23:2+s+b,24:2+idx(6)+coded(1,[20,23]),25:idx(2)+2*method,26:s,27:b,28:2+coded(1,[4,6])+s+idx(26),29:4+idx(4),30:8,31:4,32:16+b+2*s,33:4,34:12,35:12+2*b+2*s,36:4+idx(35),37:12+idx(35),38:4+s+b,39:8+2*s+impl,40:8+s+impl}
 for i in range(40):p+=rows.get(i,0)*sizes[i]
 strings=headers['#Strings'][0];found=[]
 for i in range(rows.get(40,0)):
  q=p+i*sizes[40];nameindex=int.from_bytes(data[q+8:q+8+s],'little');start=strings+nameindex;name=data[start:data.index(0,start)].decode('utf-8')
  embedded=int.from_bytes(data[q+8+s:q+8+s+impl],'little')==0;payload=None
  if embedded:
   offset=u32(q);require(offset+4<=resource_size,'resource offset');length=u32(resources+offset);require(offset+4+length<=resource_size,'resource length')
   payload=data[resources+offset+4:resources+offset+4+length]
  found.append(dict(logicalName=name,embedded=embedded,payload=payload))
 return found
def verify_resource(path):
 found=[]
 for row in manifest_resources(path.read_bytes()) or []:
  if row['logicalName']!=LOGICAL:continue
  require(row['embedded'],'resource must be embedded, not linked')
  payload=row['payload'];digest=hashlib.sha256(payload).hexdigest();require(len(payload)==239766 and digest==RESOURCE_SHA,'compiled resource size/hash mismatch');found.append(dict(logicalName=row['logicalName'],size=len(payload),sha256=digest))
 require(len(found)==1,'exact resource absent/duplicated in compiled DLL');return found[0]
