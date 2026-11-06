using P06;
using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;

// Loads the product DLL, never its entry point; all transport and packets are synthetic.
static class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
    static void Reject(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}throw new Exception(name);}
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static void Golden(int id,SkillCastTarget target,string hex,uint caster=0){hex=hex.Replace(" ","");string actual=Convert.ToHexString(MagicRules.Request(id,target,caster));Check(actual==hex,$"wire {id}/{target.Kind}: expected {hex}, got {actual}");}
    static void Protocol(){
        Golden(3,new(SkillTargetKind.None),"0600020000000000");
        Golden(3,new(SkillTargetKind.Self),"0600027856341200",0x12345678);
        Golden(1,new(SkillTargetKind.Object,0x12345678),"0600007856341200");
        Golden(132,new(SkillTargetKind.Object,0x12345678,0x1234,0x5678),"06 10 03 78 56 34 12 34 12 78 56 00 00 00 00 00");
        Golden(58,new(SkillTargetKind.Location,X:0x1234,Y:0x5678),"0607013412785600");
        Golden(63,new(SkillTargetKind.Location,X:0,Y:65535),"0607060000FFFF00");
        Golden(5,new(SkillTargetKind.Bookmark,MapId:0x1234,BookmarkId:0x12345678),"06000434127856341200000000000000");
        Golden(69,new(SkillTargetKind.Bookmark,MapId:0,BookmarkId:0),"06080400000000000000000000000000");
        Golden(116,new(SkillTargetKind.Name,Name:"甲"),"060E03A5D2000000");
        Golden(118,new(SkillTargetKind.Name,Name:"peer"),"060E057065657200");
        Golden(113,new(SkillTargetKind.Object,0x12345678,0x1234,0x5678,Text:"X"),"06 0E 00 78 56 34 12 34 12 78 56 58 00 00 00 00");
        Golden(51,new(SkillTargetKind.Summon,SummonId:0x12345678),"0606027856341200");
        // Independently enumerated validator cases ensure every row/column and layout is covered.
        int[] dhh=[4,6,7,10,15,16,17,18,22,25,28,30,34,38,45,46,50,65,74,77,108,132,184,187,192,194,202,203,207,208,213];
        for(int id=1;id<=220;id++){
            SkillCastTarget target=id switch {
                116 or 118=>new(SkillTargetKind.Name,Name:"peer"),
                5 or 69=>new(SkillTargetKind.Bookmark,MapId:4,BookmarkId:123),
                58 or 63=>new(SkillTargetKind.Location,X:123,Y:456),
                _ when dhh.Contains(id)||id==113=>new(SkillTargetKind.Object,123,123,456),
                _=>new(SkillTargetKind.Self)
            };
            byte[] b=MagicRules.Request(id,target,123);
            int semantic=id is 116 or 118?8:id is 5 or 69?9:id==113?12:dhh.Contains(id)?11:7;
            Check(b[0]==6&&b[1]*8+b[2]+1==id&&b[2]<8,"identity "+id);
            Check(b.Length==((semantic+7)&~7)&&b.Skip(semantic).All(v=>v==0),"padding "+id);
            Check(MagicSpells.Get(id)!=null&&MagicRules.Describe(id).Supported,"catalog-independent support "+id);
        }
        foreach(int id in new[]{-1,0,221,256,int.MaxValue})Reject(()=>MagicRules.Request(id,new(SkillTargetKind.Self),123),"invalid skill "+id);
        Reject(()=>MagicRules.Request(116,new(SkillTargetKind.Object,123)),"name is not DWORD");
        Reject(()=>MagicRules.Request(116,new(SkillTargetKind.Name,Name:"")),"empty name");
        Reject(()=>MagicRules.Request(116,new(SkillTargetKind.Name,Name:"a\0b")),"embedded NUL");
        Reject(()=>MagicRules.Request(116,new(SkillTargetKind.Name,Name:"😀")),"unencodable name");
        Reject(()=>MagicRules.Request(116,new(SkillTargetKind.Name,Name:new string('x',256))),"bounded name");
        Reject(()=>MagicRules.Request(58,new(SkillTargetKind.Location,X:1)),"missing Y");
        Reject(()=>MagicRules.Request(5,new(SkillTargetKind.Bookmark,MapId:0)),"missing bookmark is not random teleport");
        Reject(()=>MagicRules.Request(6,new(SkillTargetKind.None)),"attack needs object");
        Reject(()=>MagicRules.Request(6,new(SkillTargetKind.Object,0,1,2)),"zero object");
        Reject(()=>MagicRules.Request(3,new(SkillTargetKind.Self)),"missing caster");
        Reject(()=>MagicRules.Request(3,new(SkillTargetKind.Summon,SummonId:1)),"summon branch only 51");
        Reject(()=>MagicRules.Request(51,new(SkillTargetKind.Summon)),"missing summon selector");
        Reject(()=>MagicRules.Request(113,new(SkillTargetKind.Object,123,1,2,Text:"a\0b")),"mark text NUL");
        Check(MagicRules.Request(4,123,1,2).SequenceEqual(MagicRules.Request(4,new(SkillTargetKind.Object,123,1,2))),"legacy overload uses same encoder");
    }
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static byte[] Pack(uint id,ushort x=32600,byte action=0)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write((ushort)32940);b.Write(id);b.Write((ushort)0);b.Write(action);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);Text(b,id==123?"offline":"peer");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static byte[] Learned(int id,bool add=true){byte[] b=new byte[32];b[0]=(byte)(add?164:160);b[1]=32;b[2+(id-1)/8]=(byte)(1<<((id-1)%8));return b;}
    static LoginConnection Seed(int skill=132){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        Feed(c,Pack(123));Feed(c,Pack(456,32601));Feed(c,Learned(skill));c.World.SelectMagic(skill);c.World.SelectMagicTarget(456);
        return c;
    }
    static async Task Sending(){
        foreach(var row in new (int Id,SkillCastTarget? Target)[]{
            (132,null),(87,new(SkillTargetKind.Object,456)),(201,new(SkillTargetKind.Self)),
            (58,new(SkillTargetKind.Location,X:32602,Y:32941)),(63,null),
            (5,new(SkillTargetKind.Bookmark,MapId:4,BookmarkId:42)),(69,new(SkillTargetKind.Bookmark,MapId:0,BookmarkId:0)),
            (116,new(SkillTargetKind.Name,Name:"遠方盟友")),(118,null),
            (113,new(SkillTargetKind.Object,456,Text:"集中")),(51,new(SkillTargetKind.Summon,SummonId:7)),(3,new(SkillTargetKind.None))
        }){
            using var c=Seed(row.Id);int sent=0;byte[]? bytes=null;var status=c.World.Status;
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((p,valid,admit)=>{if(!valid())return Task.FromResult(false);sent++;bytes=p;admit();return Task.FromResult(true);}));
            Check(c.CanCastSelectedMagic(row.Target),"available "+row.Id);
            Check(await c.CastSelectedMagic(row.Target)&&sent==1,"send "+row.Id);
            Check(!await c.CastSelectedMagic(row.Target)&&sent==1,"single action cooldown "+row.Id);
            Check(c.World.Status==status&&c.World.MagicEffectCount==0,"no fake cost/effect "+row.Id);
            Check(bytes![1]*8+bytes[2]+1==row.Id,"sent skill "+row.Id);
            if(row.Id==132)Check(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(3))==456&&BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(7))==32601,"object uses current coordinates");
            if(row.Id==58)Check(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(3))==32602,"location has no DWORD prefix");
        }
        foreach(string change in new[]{"skill","unlearn","target","move","remove","reenter","map","death","generation","weapon","speed","input"}){
            using var c=Seed();int sent=0;var gate=new TaskCompletionSource();
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)(async(p,valid,admit)=>{await gate.Task;if(!valid())return false;sent++;admit();return true;}));
            var pending=c.CastSelectedMagic();Check(!pending.IsCompleted,"queued "+change);
            switch(change){
                case "skill":c.World.SelectMagic(null);break;
                case "unlearn":Feed(c,Learned(132,false));break;
                case "target":c.World.SelectTarget(null);break;
                case "move":Feed(c,Pack(456,32602));break;
                case "remove":Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));break;
                case "reenter":Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Feed(c,Pack(456,32601));break;
                case "map":Feed(c,LoginPackets.Pad([206,0,0,0]));break;
                case "death":Feed(c,LoginPackets.Pad([158,..BitConverter.GetBytes(123u),8]));break;
                case "generation":c.Cancel();break;
                case "weapon":Feed(c,LoginPackets.Pad([119,..BitConverter.GetBytes(123u),4]));break;
                case "speed":Set(c.World,"SpeedRevision",c.World.SpeedRevision+1);break;
                case "input":c.CancelMovementInput();break;
            }
            gate.SetResult();Check(!await pending&&sent==0,"stale request rejected "+change);
        }
        using(var c=Seed(61)){
            Feed(c,Pack(456,32601,8));Check(c.CanCastSelectedMagic(new(SkillTargetKind.Object,456)),"explicit corpse target supported");
            Feed(c,Learned(132));c.World.SelectMagic(132);Check(!c.CanCastSelectedMagic(new(SkillTargetKind.Object,456)),"attack refuses corpse");
        }
        foreach(bool explicitLocation in new[]{false,true}){
            using var c=Seed(58);int sent=0;var gate=new TaskCompletionSource();
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)(async(p,valid,admit)=>{await gate.Task;if(!valid())return false;sent++;admit();return true;}));
            var pending=c.CastSelectedMagic(explicitLocation?new(SkillTargetKind.Location,X:32601,Y:32940):null);
            Feed(c,Packet(b=>{b.Write((byte)10);b.Write(456u);b.Write((ushort)32601);b.Write((ushort)32940);b.Write((byte)2);b.Write((byte)128);}));gate.SetResult();
            Check(await pending==explicitLocation&&sent==(explicitLocation?1:0),"explicit location stays fixed; selected-object location cancels on movement "+explicitLocation);
        }
        using(var c=Seed(5)){
            Check(!c.MagicAvailable&&!await c.CastSelectedMagic(),"no implicit random teleport");
            Check(!c.World.SelectMagic(220),"unlearned selection rejected");Feed(c,Learned(220));Check(c.World.SelectMagic(220),"learned upper bound accepted");
            Feed(c,Learned(221));Check(c.World.SelectMagic(221)&&!c.MagicAvailable,"received higher skill stays unsendable");
        }
    }
    static async Task<int> Main(string[] args){try{Protocol();if(!args.Contains("--protocol-only"))await Sending();Console.WriteLine(JsonSerializer.Serialize(new{status="pass",checks=count,transport="synthetic, no sockets",nativeGameTested=false}));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
