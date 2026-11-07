using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace P06;

public static class TitleAndEmotePackets {
    public static bool IsEmote(int action)=>action is >=66 and <=69;
    public static string EmoteName(int action)=>action switch{66=>"思考",67=>"挑釁",68=>"敬禮",69=>"歡呼",_=>throw new ArgumentOutOfRangeException(nameof(action))};
    public static byte[] Title(string name,string title){
        // C_Title only requires nonempty strings. Do not invent a 16-character server rule.
        if(string.IsNullOrEmpty(name)||string.IsNullOrEmpty(title)||name.Contains('\0')||title.Contains('\0'))
            throw new ArgumentException("角色名稱與稱號不可空白或包含 NUL。");
        byte[] n=LoginPackets.TextEncoding.GetBytes(name),t=LoginPackets.TextEncoding.GetBytes(title);
        if(n.Length+t.Length+3>10232)throw new ArgumentException("稱號超過通訊封包容量。");
        return LoginPackets.Pad([90,..n,0,..t,0]);
    }
    public static byte[] Emote(int action){if(!IsEmote(action))throw new ArgumentOutOfRangeException(nameof(action));return LoginPackets.Pad([120,(byte)action]);}
    public static (uint Id,string Title) ReadTitle(LoginReader r){uint id=r.D();string title=r.S();r.Finish();return(id,title);}
    public static (uint Id,byte Action) ReadEmote(byte[] plain){var r=new LoginReader(plain);if(r.C()!=158)throw new ProtocolException("emote opcode");uint id=r.D();byte action=r.C();r.Finish();if(!IsEmote(action))throw new ProtocolException("emote action");return(id,action);}
    public static string DisplayText(string raw){string text=Regex.Replace(raw,@"\f.|[\p{Cc}]", "");return text.Length<=80?text:text[..80]+"…";}
}

public sealed record EmotePlayback(uint Id,byte Action,long At,int Epoch,object Actor,CombatAnimation? Combat);
public sealed partial class WorldSession {
    readonly Dictionary<uint,EmotePlayback> emotes=[];
    int emoteEpoch=-1;
    public string EmoteAssetNotice {get;internal set;}="";
    void ReceiveTitle(LoginReader r){
        var update=TitleAndEmotePackets.ReadTitle(r);
        if(Actor?.ObjectId==update.Id){Actor=Actor with{Title=update.Title};ObjectRevision++;}
        else UpdateObject(update.Id,o=>o with{Title=update.Title});
    }
    bool ReceiveEmote(byte[] plain){
        // Leave every other S158 action, including shop/fishing/pickup/combat, to its owner.
        if(plain.Length<6||!TitleAndEmotePackets.IsEmote(plain[5]))return false;
        var update=TitleAndEmotePackets.ReadEmote(plain);StartEmote(update.Id,update.Action);return true;
    }
    internal void StartEmote(uint id,byte action){
        PruneEmotes(ActionClock());object? actor=null;CombatAnimation? combat=null;
        if(Actor?.ObjectId==id&&!OwnDead&&!SelfAppearancePending){actor=Actor;combat=OwnCombat;CancelPickupPresentation("emote");}
        else if(objects.TryGetValue(id,out var other)&&!other.Dead&&!other.AppearancePending&&!other.ShopActive){actor=other;combat=other.Combat;}
        if(actor!=null)emotes[id]=new(id,action,ActionClock(),WorldEpoch,actor,combat);
    }
    void PruneEmotes(long now){
        if(emoteEpoch!=WorldEpoch){emotes.Clear();EmoteAssetNotice="";emoteEpoch=WorldEpoch;}
        foreach(var e in emotes.Values.ToArray()){
            bool current=e.Actor is OwnActor own?ReferenceEquals(Actor,own)&&!OwnDead&&ReferenceEquals(OwnCombat,e.Combat)
                :e.Actor is WorldObject o&&objects.TryGetValue(e.Id,out var live)&&ReferenceEquals(live,o)&&!live.Dead;
            if(!current||now-e.At>=10000||now<e.At)emotes.Remove(e.Id);
        }
    }
    // Title (+240) for the name painter: S183 or S87 value, display-cleaned; empty when none.
    public string DisplayTitle(uint id)=>TitleAndEmotePackets.DisplayText(Actor?.ObjectId==id?Actor.Title:objects.TryGetValue(id,out var o)?o.Title:"");
    public EmotePlayback[] ActiveEmotes(long now){PruneEmotes(now);return emotes.Values.ToArray();}
    internal void EndEmote(uint id)=>emotes.Remove(id);
}

public sealed record TitleEmoteStamp(int Generation,int Epoch,uint Actor);
public sealed partial class LoginConnection {
    TitleEmoteStamp? titleEmotePending;
    public TitleEmoteStamp? CaptureTitleEmote()=>CaptureWorld(()=>World.Actor is {} a?new TitleEmoteStamp(generation,World.WorldEpoch,a.ObjectId):null);
    public Task<bool> SendTitle(TitleEmoteStamp? stamp,string name,string title)=>SendTitleEmote(stamp,()=>TitleAndEmotePackets.Title(name,title),null);
    public Task<bool> SendEmote(TitleEmoteStamp? stamp,int action)=>SendTitleEmote(stamp,()=>TitleAndEmotePackets.Emote(action),(byte)action);
    async Task<bool> SendTitleEmote(TitleEmoteStamp? stamp,Func<byte[]> encode,byte? action){
        bool Current()=>stamp!=null&&stamp.Generation==generation&&stamp.Epoch==World.WorldEpoch&&stamp.Actor==World.Actor?.ObjectId;
        bool Ready()=>Current()&&State==LoginState.InWorld&&World.Ready&&!World.ActionLocked&&!World.TradeBlocksActions&&!World.OwnDead&&World.Status?.Hp>0&&!MovementBusy
            &&(action==null||World.Actor is {} a&&(a.Flags&2)==0&&a.Weapon!=70);
        if(stamp==null||titleEmotePending==stamp||sendMovementGate==null||!CaptureWorld(Ready))return false;
        byte[] bytes;try{bytes=encode();}catch(ArgumentException){return false;}
        int epoch=generation;var sender=sendMovementGate;titleEmotePending=stamp;
        try{return await sender(bytes,()=>CaptureWorld(Ready),()=>{
            // broadcastPacketAll excludes self; local playback is explicitly predicted after admission.
            if(action is byte value&&World.Actor is {} actor)World.StartEmote(actor.ObjectId,value);
        })&&CaptureWorld(Current);}
        catch(Exception){if(epoch==generation){Cancel();Set(LoginState.Faulted,"稱號／表情傳送失敗，已斷線；不會自動重送。");}return false;}
        finally{if(titleEmotePending==stamp)titleEmotePending=null;}
    }
}

// Reads the original indexed SPR in place. No artwork or generated cache is written to the repo.
public sealed class EmoteSprites:IDisposable {
    public const string MetadataSha="28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8";
    sealed record ArchivePin(string Name,string IndexSha,string PakSha,long Size);
    // Same installed archive identities as GroundSpriteReader; artwork remains external.
    static readonly ArchivePin[] pins=[
        new("Sprite","327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b","4dd4e5348fbad2e69bae9cc388be23d7becebe8423ad9f79f393c1457a69ee62",1904012586L),
        new("Sprite00","15b46ae6995b5b3d64a27a509c78847f0329ba55360a200961a7ecb73b13c96c","64741119e853880e0e31009cf06145a5f21520b5264d07ece5541ba701a01c9d",450577933L),
        new("Sprite01","1e4c819debe37449691142025692ac1ff1d1f82c148830c76338e7fdff8cdd8f","a46a5a85e56c03cc6b1d7ed7cddf3d59e8887612313842cde9822b76932b5ac5",422714941L),
        new("Sprite02","a0208077cfa7e76280641f1a2bf7f7aeacc43b6f9a16e21771ebd732e791ae5c","88858f8eaf57854d50ea21f2d84dd3d3e8c1d3fb30c5c583af2175fc465cde0c",412811463L),
        new("Sprite03","4deb893637d40e1a9e91844a4a3274f9de25e7af42197e0ac69773241f233bf6","c8ee2e21f78c69eae9307db0dd7e721f929581ad07c386e984ee4735febb6c59",422961934L),
        new("Sprite04","6ea2246d89bfd1dc7ee3d4b70a36f0bf700889d1eb5f4cad9ab633a72bf539f9","ece9df4053fb06eab08f183d8c0b555de0ec04262fdae87f9775d9ce81ca82eb",396502716L),
        new("Sprite05","f4c6d5d7974ad355b066faf620aea9428104fa64274b5eeb24ce60bf54030158","606792a3a484065de833b1fcdaa1dc64f9b134da38945fc76084ce536f4d89a1",370792959L),
        new("Sprite06","0248238be94b99e61d2c515d59066b445af9faf5b3430132bbdce706d6812bcf","5ab3fc86d66557ebb0810228174b3702d7e07cd4bd766dfb1874661f7567d861",399491486L),
        new("Sprite07","ce996c526474647be2986c2610afe2e6b193f9d24069033615ffafb3a9136a60","30a4c7eccb62b3bec7d04a2891b8239e9bd98b3266e7406d0290d5ec7ab82065",428993498L),
        new("Sprite08","4f96ce84bcb0f733842b28b3dcb39984828985f6af994ce88aae214162782022","3d23ee72b9d70c61d1cd156e2ad99e4a100d46b13324c6440deb8d6386f1d8ef",405690494L),
        new("Sprite09","37edc3cbfb37ae667aa61a6ff3c141b7a7b351f60b6990d3bcee5c5eb55f8f9a","7256514d893846361b5e6854f5129a3f9e662fa44e41c3a4d7b44c0ce4a98b36",409910541L),
        new("Sprite10","589c47ecb1a56dc9d0956ad7716e599a00f68290005f401bbc595879e253ecb1","966089b4aac89ccf139833492b96ddc8c8aa342d8d0506d74f9de43c1571a128",401362413L),
        new("Sprite11","4d560607d2b290992d0cd6beaa7556cd6d03a009346418915636c01e06c20e88","cbcab1f612218ada11589a678f9ea85a07e393f62dea24529aded1c92712343e",408627491L),
        new("Sprite12","ada81449a16e323f90539cdf0571e59f120c52629855a9fe536e19c8e34481fe","a5f735d10a891150da0b0f9b97c81a0c8e8f3ba269e1595e1cc2352bd8ac9a7c",392469796L),
        new("Sprite13","e03cf669b50ee8fb86ac4e3242fb68a211580f78f8929770a9077d4fa776203b","216e300c8b6d0d7d312f95f2dc4323f9246c1d14a6cba63a0fc7ed172510408f",404075215L),
        new("Sprite14","c6d10d9c14ecac20c5b96546f9b4494eb23cc9b28c1ab07a96070eb574e84c9a","ba14f7a681a690deda925f812bcbfeb6af392bab7b292a0e48742455f933dcc8",442915587L),
        new("Sprite15","874b53afe762c43415e25204f4e8fcf405f36adfaca6b9258bd84c1eb1d4c441","864800d2afbd8b38ed9bfcc08a0079b27fbf53f018de562f4cba6fe54dccabd2",484407291L),
    ];
    readonly string root;
    Dictionary<int,string>? rows;
    readonly Dictionary<string,Dictionary<string,(uint Offset,uint Length)>> indexes=[];
    readonly Dictionary<string,FileStream> archives=[];
    readonly Dictionary<(int,int,int),EmoteSequence> sequences=[];
    readonly Dictionary<string,Frame[]> sprites=[];
    public EmoteSprites(string? root=null){this.root=root??GroundSpriteReader.ClientRoot;}
    static void Require(bool ok){if(!ok)throw new InvalidDataException("表情原圖格式或來源 SHA 不符");}
    static bool Hash(byte[] data,string sha)=>Convert.ToHexString(SHA256.HashData(data)).Equals(sha,StringComparison.OrdinalIgnoreCase);
    public sealed record Step(int File,int Frame,int Milliseconds);
    public sealed record EmoteSequence(int Sprite,Step[] Steps,Frame[] Frames){
        public int Duration=>Steps.Sum(s=>s.Milliseconds);
        public Frame? At(long age){if(age<0||age>=Duration)return null;for(int i=0;i<Steps.Length;i++){if(age<Steps[i].Milliseconds)return Frames[i];age-=Steps[i].Milliseconds;}return null;}
    }
    public static Step[] ParseSteps(string row,int action,int direction){
        Require(TitleAndEmotePackets.IsEmote(action)&&direction is >=0 and <8);
        int fps=24;string? body=null;int count=0;
        foreach(string line in row.Split('\n')){
            var rate=Regex.Match(line,@"^\s*110\.[^(]+\((\d+)\)");if(rate.Success)fps=int.Parse(rate.Groups[1].Value);
            // The pinned gfx2796/action66 row contains a doubled opening parenthesis.
            var m=Regex.Match(line,@"^\s*"+action+@"\.[^(]+\(\(?1\s+(\d+),([^)]*)\)");
            if(m.Success){body=m.Groups[2].Value;count=int.Parse(m.Groups[1].Value);break;}
        }
        Require(body!=null&&fps is >0 and <=240&&count is >0 and <=512);
        var tokens=body!.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);Require(tokens.Length==count);
        var steps=new List<Step>();
        foreach(string token in tokens){var m=Regex.Match(token,@"^(\d+)\.(\d+):(\d+)(?:[!<\[\]>^].*)?$");Require(m.Success);
            int file=int.Parse(m.Groups[1].Value)+direction,frame=int.Parse(m.Groups[2].Value),ticks=int.Parse(m.Groups[3].Value);
            Require(file<=65535&&frame<128&&ticks<=1000);steps.Add(new(file,frame,ticks*1000/fps));}
        Require(steps.Sum(s=>s.Milliseconds) is >0 and <=10000);return steps.ToArray();
    }
    public EmoteSequence Load(int gfx,int action,int direction){
        var key=(gfx,action,direction);if(sequences.TryGetValue(key,out var cached))return cached;
        if(rows==null){byte[] data=File.ReadAllBytes(Path.Combine(root,"TW13081901.txt"));Require(Hash(data,MetadataSha));
            rows=Regex.Matches(Encoding.ASCII.GetString(data),@"(?ms)^#(\d+)[ \t]+.*?(?=^#\d+[ \t]+|\z)").ToDictionary(m=>int.Parse(m.Groups[1].Value),m=>m.Value);}
        if(!rows.TryGetValue(gfx,out var row))throw new InvalidDataException("表情動作索引缺件");
        var steps=ParseSteps(row,action,direction);int sprite=gfx;var visited=new HashSet<int>();
        while(rows.TryGetValue(sprite,out var aliasRow)){
            Require(visited.Add(sprite));var alias=Regex.Match(aliasRow,@"^#\d+\s+\d+\s*=\s*(\d+)");if(!alias.Success)break;sprite=int.Parse(alias.Groups[1].Value);
        }
        var frames=steps.Select(s=>{var available=ReadSprite(sprite,s.File);Require(s.Frame<available.Length);return available[s.Frame];}).ToArray();
        var result=new EmoteSequence(sprite,steps,frames);sequences.Add(key,result);return result;
    }
    Frame[] ReadSprite(int sprite,int file){
        string name=$"{sprite}-{file}.spr";if(sprites.TryGetValue(name,out var cached))return cached;
        foreach(var pin in pins){
            if(!indexes.TryGetValue(pin.Name,out var index)){
                string path=Path.Combine(root,pin.Name+".idx");if(!File.Exists(path))continue;
                byte[] data=File.ReadAllBytes(path);Require(Hash(data,pin.IndexSha));
                using var r=new BinaryReader(new MemoryStream(data));uint count=r.ReadUInt32();Require(4L+28L*count==data.Length);
                index=new(StringComparer.OrdinalIgnoreCase);for(int i=0;i<count;i++){uint offset=r.ReadUInt32();byte[] text=r.ReadBytes(20);int end=Array.IndexOf(text,(byte)0);Require(end>0);string entry=Encoding.ASCII.GetString(text,0,end);uint size=r.ReadUInt32();Require((long)offset+size<=pin.Size&&index.TryAdd(entry,(offset,size)));}
                indexes.Add(pin.Name,index);
            }
            if(!index.TryGetValue(name,out var item))continue;
            if(!archives.TryGetValue(pin.Name,out var pak)){
                var stream=new FileStream(Path.Combine(root,pin.Name+".pak"),FileMode.Open,FileAccess.Read,FileShare.Read);
                try{Require(stream.Length==pin.Size&&Convert.ToHexString(SHA256.HashData(stream)).Equals(pin.PakSha,StringComparison.OrdinalIgnoreCase));pak=stream;archives.Add(pin.Name,pak);}catch{stream.Dispose();throw;}
            }
            Require(item.Length is >0 and <=8*1024*1024&&(long)item.Offset+item.Length<=pak.Length);
            byte[] payload=new byte[item.Length];pak.Position=item.Offset;pak.ReadExactly(payload);var result=Decode(payload,file);sprites.Add(name,result);return result;
        }
        throw new InvalidDataException("表情原圖缺件："+name);
    }
    public static Frame[] Decode(byte[] payload,int file){
        try{
            Require(payload.Length is >0 and <=8*1024*1024);using var r=new BinaryReader(new MemoryStream(payload));int count=r.ReadByte();ushort[]? palette=null;
            if(count==255){int n=r.ReadByte();if(n==0)n=256;palette=Enumerable.Range(0,n).Select(_=>r.ReadUInt16()).ToArray();Require(palette.All(c=>c<32768));count=r.ReadByte();}
            Require(count is >0 and <=127);var headers=new List<(int[] Bounds,(int A,int B,int Z,int Index)[] Tiles)>();
            int total=0;for(int i=0;i<count;i++){
                int[] bounds=[r.ReadInt16(),r.ReadInt16(),r.ReadInt16(),r.ReadInt16()];int u=r.ReadUInt16(),v=r.ReadUInt16(),n=r.ReadInt16();total+=n;
                bool empty=bounds.SequenceEqual(new[]{192,120,-128,-120})&&u==65535&&v==0&&n==0;
                Require(empty||(bounds[2]>=bounds[0]&&bounds[3]>=bounds[1]&&bounds[2]-bounds[0]<2048&&bounds[3]-bounds[1]<2048&&n is >0 and <=4096));Require(total<=65536);
                var tiles=new (int,int,int,int)[n];for(int j=0;j<n;j++)tiles[j]=(r.ReadSByte(),r.ReadSByte(),r.ReadByte(),r.ReadUInt16());headers.Add((bounds,tiles));
            }
            uint blocks=r.ReadUInt32();Require(blocks is >0 and <=10000);uint[] offsets=new uint[blocks];for(int i=0;i<blocks;i++)offsets[i]=r.ReadUInt32();uint expected=r.ReadUInt32();long start=r.BaseStream.Position;int expanded=0,pixels=0;var decoded=new List<Tile>();
            for(int i=0;i<blocks;i++){
                Require(r.BaseStream.Position-start==offsets[i]);int sx=r.ReadByte(),sy=r.ReadByte(),width=r.ReadByte(),lines=r.ReadByte();expanded+=4;Require(width>0&&lines>0&&sx+width<=24&&sy+lines<=24);var values=new List<Pixel>();
                for(int y=sy;y<sy+lines;y++){int x=sx,segments=r.ReadByte();expanded++;for(int j=0;j<segments;j++){
                    int skip=r.ReadByte(),run=r.ReadByte();expanded+=2;Require(skip%2==0);x+=skip/2;Require(run>0&&x+run<=sx+width);
                    for(int k=0;k<run;k++){int color;if(palette==null)color=r.ReadUInt16();else{int p=r.ReadByte();Require(p<palette.Length);color=palette[p];}Require(color<32768&&++pixels<=2000000);values.Add(new(x++,y,(ushort)color));expanded+=2;}
                }}decoded.Add(new(2,values.ToArray()));
            }
            Require(r.BaseStream.Position==payload.Length&&expanded==expected&&headers.All(h=>h.Tiles.All(t=>t.Index<decoded.Count)));
            return headers.Select((h,i)=>new Frame($"{file}:{i}",h.Bounds,h.Tiles.Select((t,j)=>new Block(t.A,t.B,t.Z,j,decoded[t.Index])).ToArray())).ToArray();
        }catch(EndOfStreamException ex){throw new InvalidDataException("表情原圖截斷",ex);}
    }
    public void Dispose(){foreach(var archive in archives.Values)archive.Dispose();archives.Clear();}
}

public sealed partial class Renderer {
    // A single ObjectRendering hook owns the final title overlay and original emote substitution.
    internal Patch ComposeTitlesAndEmotes(World own,WorldSession session,ObjectAssets catalog,Patch original,List<ObjectVisual> visuals,long now){
        var commands=original.Commands;bool changed=false;
        foreach(var emote in session.ActiveEmotes(now)){
            bool self=emote.Actor is OwnActor;int gfx,direction,x,y,weapon;
            if(emote.Actor is OwnActor a){gfx=a.Gfx;direction=a.Direction;x=a.X;y=a.Y;weapon=a.Weapon;if(own.Walking){session.EndEmote(emote.Id);continue;}}
            else{var o=(WorldObject)emote.Actor;gfx=o.Gfx;direction=o.Direction;x=o.X;y=o.Y;weapon=o.Action;if(!visuals.Any(v=>v.ObjectId==o.ObjectId&&!v.Walking))continue;}
            try{
                var sequence=EmoteCache.Get(gfx,emote.Action,direction);var frame=sequence.At(now-emote.At);
                if(frame==null){session.EndEmote(emote.Id);continue;}
                uint drawId=self?0:emote.Id;commands.RemoveAll(d=>d.ObjectId==drawId&&d.Layer is "actor" or "shadow");
                commands.AddRange(frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile,drawId)));
                var appearance=catalog.Get(gfx,weapon);commands.AddRange(ShadowDraws(appearance,frame,x,y,drawId));
                var visual=new ObjectVisual(emote.Id,gfx,sequence.Sprite,self?session.Actor!.Name:((WorldObject)emote.Actor).Name,x,y,"emote-"+emote.Action,frame.Key,Coordinates.Screen(x,y),Rectangle.Empty,false,sequence.Duration);
                if(self)OwnCombatVisual=visual;else{int i=visuals.FindIndex(v=>v.ObjectId==emote.Id);if(i>=0)visuals[i]=visual;}
                changed=true;
            }catch(Exception ex) when(ex is IOException or InvalidDataException or IndexOutOfRangeException){session.EmoteAssetNotice="無法顯示表情原圖："+ex.Message;session.EndEmote(emote.Id);}
        }
        // Titles are no longer baked into the scene: ObjectRendering draws title, name and emblem for the
        // hovered object only (actions-and-names.md 2.2). This hook now owns emote substitution alone.
        if(!changed)return original;
        var area=original.Bounds;
        foreach(var d in commands.Where(d=>d.Layer=="actor"))area=Rectangle.Union(area,new Rectangle(d.Px,d.Py,24,24));area=Rectangle.Intersect(area,Bounds);
        // Refresh scenery for an emote extending beyond the previous pose, preserving tile depth/ownership.
        var scenery=ActiveScene.ToHashSet();commands=commands.Where(d=>!scenery.Contains(d)).Concat(ActiveScene.Where(d=>d.Px<area.Right&&d.Px+24>area.Left&&d.Py<area.Bottom&&d.Py+24>area.Top)).OrderBy(d=>d,DrawComparer.Instance).ThenBy(d=>d.ObjectId).ToList();
        // ComposeAfflictionPatch is the only owner of tint, blindness and opacity.
        return ComposeAfflictionPatch(session,area,commands,session.Opacity);
    }
}

static class EmoteCache {
    static readonly object gate=new();
    static readonly EmoteSprites reader=new();
    static readonly Dictionary<(int,int,int),EmoteSprites.EmoteSequence> cache=[];
    public static EmoteSprites.EmoteSequence Get(int gfx,int action,int direction){lock(gate){var key=(gfx,action,direction);if(cache.TryGetValue(key,out var result))return result;result=reader.Load(gfx,action,direction);cache.Add(key,result);return result;}}
}

public sealed partial class ClientForm {
    bool titleEmoteUiHooked;
    // The On* lifecycle overrides belong to other modules; subscribe before the first show so Shown cannot be missed.
    protected override void SetVisibleCore(bool value){
        if(value&&!titleEmoteUiHooked){titleEmoteUiHooked=true;Shown+=(_,_)=>InitTitleEmoteUi();}
        base.SetVisibleCore(value);
    }
    void InitTitleEmoteUi(){
        if(normalPanels!=null){
            var page=new TabPage("稱號／表情"){AutoScroll=true};var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};page.Controls.Add(layout);
            var name=new TextBox{Width=260,AccessibleName="授予稱號的角色名稱",MaxLength=10232};var title=new TextBox{Width=260,AccessibleName="稱號",MaxLength=10232};
            var notice=new Label{Width=390,Height=100,Text="稱號權限與等級由伺服器判定。表情依原圖播放。"};var submit=new Button{Text="送出稱號",AutoSize=true};
            layout.Controls.AddRange([new Label{Text="角色名稱",AutoSize=true},name,new Label{Text="稱號",AutoSize=true},title,submit]);
            TitleEmoteStamp? stamp=null;int lastEpoch=-1,lastGeneration=-1;
            page.Enter+=(_,_)=>{ClearInput("title-emote");stamp=login.CaptureTitleEmote();name.Text=login.World.Actor?.Name??"";title.Text=login.World.Actor?.Title??"";};
            submit.Click+=async(_,_)=>{ClearInput("title");var request=stamp;submit.Enabled=false;try{TitleAndEmotePackets.Title(name.Text,title.Text);bool sent=await login.SendTitle(request,name.Text,title.Text);if(request==login.CaptureTitleEmote())notice.Text=sent?"稱號請求已送出；等待伺服器更新或拒絕訊息。":"未送出：角色或操作狀態已變更，請重新開啟此頁。";}catch(ArgumentException ex){notice.Text=ex.Message;}finally{submit.Enabled=true;}};
            var menu=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=260,AccessibleName="表情選單"};menu.Items.AddRange(Enumerable.Range(66,4).Select(a=>(object)TitleAndEmotePackets.EmoteName(a)).ToArray());menu.SelectedIndex=0;
            var play=new Button{Text="播放表情",AutoSize=true};layout.Controls.AddRange([menu,play,notice]);
            play.Click+=async(_,_)=>{ClearInput("emote");var request=stamp;play.Enabled=false;try{bool sent=!World.Walking&&await login.SendEmote(request,66+menu.SelectedIndex);if(request==login.CaptureTitleEmote())notice.Text=sent?"表情已送出；自身動畫為本地預測，他人可見與否依伺服器判定。":"未送出：請停止移動並確認角色可操作。";}finally{play.Enabled=true;}};
            timer.Tick+=(_,_)=>{if(lastEpoch!=login.World.WorldEpoch||lastGeneration!=login.SessionGeneration){lastEpoch=login.World.WorldEpoch;lastGeneration=login.SessionGeneration;stamp=null;name.Clear();title.Clear();notice.Text="請重新選取稱號／表情頁面。";}if(login.World.EmoteAssetNotice.Length>0)notice.Text=login.World.EmoteAssetNotice;};
            normalPanels.TabPages.Add(page);
        }
    }
}
