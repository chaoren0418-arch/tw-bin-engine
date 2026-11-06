using System.Text.Json;
namespace P06;
public sealed record OpacityNode(string Layer,int Serial,int InitialFlags);
public sealed record OpacityGroup(string Key,string Block,int Ordinal,int Id,int Offset,int[] Nodes);
public sealed record OpacityTrigger(string Key,int Bits,int Offset);
public sealed class OpacityCatalog {
    public static readonly Lazy<OpacityCatalog> Shared=new(()=>new(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets"))));
    public OpacityNode[] Nodes {get;}
    public Dictionary<(string,int),int> NodeIndex {get;}=[];
    public Dictionary<string,OpacityGroup> Groups {get;}=[];
    public Dictionary<(int,int),OpacityTrigger[]> Cells {get;}=[];
    public OpacityCatalog(string root){
        var metadata=Json.Read(Path.Combine(root,"scene/opacity.json"));if(metadata.S("schema")!="p23-original-opacity-v1")throw new InvalidDataException("opacity schema");
        var types=Json.Read(Path.Combine(root,"scene/tile-pixels.json")).EnumerateArray().ToDictionary(t=>(t.I("tile"),t.I("index")),t=>t.I("type"));
        var list=new List<OpacityNode>();var first=new Dictionary<(int,int,int,int),int>();
        // Original group lookup selects the first z-sorted render node with the same tile pointer.
        foreach(var c in Json.Read(Path.Combine(root,"scene/draw-commands.json")).EnumerateArray()){
            if(c.S("layer")=="ground"||c.I("x")+256*c.I("y")>=33540)continue;
            int n=list.Count;list.Add(new(c.S("layer"),c.I("serial"),(types[(c.I("tile"),c.I("index"))]&4)!=0?256:0));NodeIndex.Add((c.S("layer"),c.I("serial")),n);first.TryAdd((c.I("x"),c.I("y"),c.I("tile"),c.I("index")),n);
        }
        Nodes=list.ToArray();
        foreach(var g in metadata.GetProperty("groups").EnumerateArray()){
            var ids=new List<int>();foreach(var o in g.GetProperty("objects").EnumerateArray())if(first.TryGetValue((o.I("x"),o.I("globalY"),o.I("tile"),o.I("index")),out int n))ids.Add(n);
            Groups.Add(g.S("key"),new(g.S("key"),g.S("block"),g.I("ordinal"),g.I("id"),g.I("offset"),ids.ToArray()));
        }
        var cells=new Dictionary<(int,int),List<OpacityTrigger>>();
        foreach(var t in metadata.GetProperty("triggers").EnumerateArray()){
            if(t.I("type") is not (0 or 1)||t.I("bits")!=(1<<t.I("type"))||!Groups.ContainsKey(t.S("key")))throw new InvalidDataException("Unknown opacity source/type");
            if(!t.GetProperty("admittedByColdSlot").GetBoolean())continue;
            var cell=(t.I("globalX"),t.I("globalY"));if(!cells.TryGetValue(cell,out var ts))cells[cell]=ts=[];ts.Add(new(t.S("key"),t.I("bits"),t.I("offset")));
        }
        foreach(var p in cells)Cells[p.Key]=p.Value.ToArray();
    }
    public OpacityCatalog(OpacityNode[] nodes,IEnumerable<OpacityGroup> groups,Dictionary<(int,int),OpacityTrigger[]> cells){Nodes=nodes;for(int i=0;i<nodes.Length;i++)NodeIndex.Add((nodes[i].Layer,nodes[i].Serial),i);foreach(var g in groups)Groups.Add(g.Key,g);foreach(var c in cells){if(c.Value.Any(t=>!Groups.ContainsKey(t.Key)))throw new InvalidDataException("Stale cache trigger group");Cells.Add(c.Key,c.Value);}}
}
public readonly record struct OpacityValue(int Flags,int Alpha,int Target){public bool Transition=>(Flags&0xc00)!=0;public bool Hidden=>!Transition&&(Flags&2)!=0;public bool Half=>!Transition&&(Flags&0x101)!=0;}
public sealed class SceneOpacity {
    OpacityCatalog data;OpacityValue[] values;readonly HashSet<string> active=[];
    readonly List<(long At,string Key,bool Down,long Order)> timers=[];long order;
    (int X,int Y)? position;long time;
    public int Revision {get;private set;}
    public SceneOpacity(OpacityCatalog catalog){data=catalog;values=catalog.Nodes.Select(n=>new OpacityValue(n.InitialFlags,0,0)).ToArray();}
    public void Rebind(OpacityCatalog catalog){var previous=data;var old=values;values=catalog.Nodes.Select(n=>previous.NodeIndex.TryGetValue((n.Layer,n.Serial),out int i)?old[i]:new OpacityValue(n.InitialFlags,0,0)).ToArray();data=catalog;active.RemoveWhere(k=>!data.Groups.ContainsKey(k));timers.RemoveAll(t=>!data.Groups.ContainsKey(t.Key));Revision++;}
    public OpacityValue Get(Draw c)=>data.NodeIndex.TryGetValue((c.Layer,c.Serial),out int n)?values[n]:new(Renderer.InitialFlags(c),0,0);
    void Cancel(string key,bool down)=>timers.RemoveAll(t=>t.Key==key&&t.Down==down);
    void Schedule(string key,bool down)=>timers.Add((time+50,key,down,order++));
    public void Move(int x,int y,long now){
        Advance(now);var old=position;position=(x,y);
        var previous=old is {} p&&data.Cells.TryGetValue((2*(p.X-Coordinates.MinX),p.Y-Coordinates.MinY),out var a)?a:[];
        var current=data.Cells.TryGetValue((2*(x-Coordinates.MinX),y-Coordinates.MinY),out var b)?b:[];
        foreach(var t in previous)active.Remove(t.Key);
        foreach(var t in current){Enter(t.Key,t.Bits);if(!active.Contains(t.Key))Leave(t.Key);}
        foreach(var t in previous)if(!active.Contains(t.Key))Leave(t.Key);
        Revision++;
    }
    void Enter(string key,int bits){
        active.Remove(key);bool down=false,up=false;
        foreach(int i in data.Groups[key].Nodes){var s=values[i];int f=s.Flags,a=s.Alpha,v=s.Target;
            if((bits&2)!=0&&(f&0x402)==0){if((f&0x800)==0)a=(f&0x101)!=0?16:31;f=(f&~0x800)|0x400;v=2;down=true;}
            else if((bits&1)!=0&&(f&0x403)==0){if((f&0x800)==0)a=(f&0x100)!=0?16:31;f=(f&~0x800)|0x400;v=1;down=true;}
            else if((f&0x800)!=0){f=(f&~0x800)|0x400;v=bits;down=true;}
            else if((f&0x400)!=0){f&=~0x800;v=bits;down=true;}
            else if((bits&1)!=0&&(f&2)!=0){f=(f&~0x400)|0x800;v=bits;up=true;}
            else{a=0;f=(f&unchecked((int)0xfffff3fc))|bits;} // Original85E090 leaves target unchanged in this terminal branch.
            values[i]=new(f,a,v);active.Add(key);
        }
        if(down){Cancel(key,false);Schedule(key,true);}else if(up){Cancel(key,false);Cancel(key,true);Schedule(key,false);}
    }
    void Leave(string key){
        bool up=false;
        foreach(int i in data.Groups[key].Nodes){var s=values[i];int f=s.Flags,a=s.Alpha;
            if((f&2)!=0&&(f&0x800)==0){if((f&0x400)==0)a=1;f=(f&~0x400)|0x800;up=true;}
            else if((f&1)!=0&&(f&0x800)==0){if((f&0x400)==0)a=17;f=(f&~0x400)|0x800;up=true;}
            else if((f&0x400)!=0){f=(f&~0x400)|0x800;up=true;}
            else if((f&0x800)==0){f&=unchecked((int)0xfffffbfc);a=0;}
            values[i]=new(f,a,0);
        }
        if(up){Cancel(key,true);Schedule(key,false);}
    }
    void Tick(string key,bool down){
        Cancel(key,down);bool again=false;
        foreach(int i in data.Groups[key].Nodes){var s=values[i];int f=s.Flags,a=s.Alpha,v=s.Target;
            if(down&&(f&0x400)!=0){
                if((v&2)!=0&&a>0){a-=4;if(a>=0)again=true;else{a=0;f=(f&unchecked((int)0xfffffbfc))|v;}}
                else if((v&1)!=0&&a>15){a-=4;if(a>=17)again=true;else{a=0;f=(f&unchecked((int)0xfffffbfc))|v;}}
                else{a=0;f=(f&unchecked((int)0xfffffbfc))|v;}
            }else if(!down&&(f&0x800)!=0){a+=4;if((v&1)!=0||(f&0x100)!=0){if(a<=16)again=true;else{v=a=0;f=(f&unchecked((int)0xfffff7fd))|1;}}else{if(a<=31)again=true;else{v=a=0;f&=unchecked((int)0xfffff7fc);}}}
            values[i]=new(f,a,v);
        }
        if(again)Schedule(key,down);Revision++;
    }
    public void Advance(long now){
        if(now<time)throw new InvalidOperationException("Opacity monotonic clock reversed");
        // Original A23360 pumps callbacks at current A228B0 time; A231C0 rearms from now.
        time=now;
        while(timers.Count>0){var next=timers.OrderBy(t=>t.At).ThenBy(t=>t.Order).First();if(next.At>now)break;timers.Remove(next);Tick(next.Key,next.Down);}
    }
    public object Snapshot()=>new{revision=Revision,time,position=position is {} p?new[]{p.X,p.Y}:null,groups=active.Order().ToArray(),states=values.Select((s,i)=>new{s,i}).Where(v=>(v.s.Flags&0xc03)!=0||v.s.Alpha!=0||v.s.Target!=0).Select(v=>new object[]{data.Nodes[v.i].Layer,data.Nodes[v.i].Serial,v.s.Flags,v.s.Alpha,v.s.Target}).ToArray(),timers=timers.OrderBy(t=>t.At).ThenBy(t=>t.Order).Select(t=>new{at=t.At,direction=t.Down?"down":"up",key=t.Key}).ToArray()};
}
public sealed partial class WorldSession {
    public bool CacheEnabled {get;set;}
    public OriginalMapCache? MapCache {get;private set;}
    SceneOpacity? sceneOpacity;
    internal Func<long> SceneClock=()=>Environment.TickCount64;
    public SceneOpacity? Opacity=>sceneOpacity;
    public event Action<object>? OpacityObserved;
    void ObserveOpacity(string action)=>OpacityObserved?.Invoke(new{action,epoch=WorldEpoch,opacity=sceneOpacity?.Snapshot(),cache=MapCache?.Snapshot()});
    void ResetOpacity(){Clear6016(SceneClock());InvalidateMapLoad();sceneOpacity=null;MapCache=null;MapWaiting();ObserveOpacity("reset");}
    void UpdateOpacity(){
        if(Actor is not {} a||MapId is not ushort map){ResetOpacity();return;}
        try{
            long now=SceneClock();sceneOpacity?.Advance(now);
            if(CacheEnabled){
                // A distant authoritative correction cannot be satisfied by one adjacent 2x2 shift.
                if(MapCache==null||MapCache.MapId!=map||!MapCache.Contains(a.X,a.Y)){
                    if(MapCache!=null)ResetOpacity();
                    if(AsyncMapLoading){RequestMapLoad(a,map);return;}MapResourceState="loading";MapResourceNotice=$"載入地圖 {map}…";var fresh=new OriginalMapCache(MapAssetsRoot,a.X,a.Y,map);MapCache=fresh;sceneOpacity=new(fresh.Catalog);
                }
                else if(MapCache.Move(a.X,a.Y))sceneOpacity!.Rebind(MapCache.Catalog);
                MapCache.ActivateEffects(now);Sync6016(now);
            }else if(map!=0||!Coordinates.Inside(a.X,a.Y)){ResetOpacity();MapResourceState="unavailable";MapResourceNotice="此預覽設定未啟用地圖供應。";return;}
            sceneOpacity??=new(OpacityCatalog.Shared.Value);sceneOpacity.Move(a.X,a.Y,now);MapResourceState="ready";MapResourceNotice=$"地圖 {map} 已載入原始區塊。";ObserveOpacity("move");
        }catch(Exception ex) when(ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or KeyNotFoundException or OverflowException or JsonException or FormatException or InvalidOperationException){MapFailed(ex);}
    }
    public void AdvanceScene(){CheckTeleportTimeout();if(sceneOpacity is {} s){int before=s.Revision;s.Advance(SceneClock());if(before!=s.Revision)ObserveOpacity("advance");}}
}

