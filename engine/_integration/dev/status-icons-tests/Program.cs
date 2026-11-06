using P06;
using System.Reflection;

static class Checks {
    static int checks;static long now=10000;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static byte[] Packet(byte opcode,Action<BinaryWriter> body){using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(opcode);body(writer);return LoginPackets.Pad(stream.ToArray());}
    static byte[] Shield(int icon,int seconds)=>Packet(216,b=>{b.Write((ushort)seconds);b.Write((byte)icon);});
    static byte[] Box(int sub,Action<BinaryWriter> body)=>Packet(250,b=>{b.Write((byte)sub);body(b);});
    static byte[] Aura(int icon,int seconds,int variant=0)=>Box(22,b=>{b.Write((byte)icon);b.Write((ushort)seconds);b.Write((byte)variant);});
    static byte[] Proxy(int key,int seconds,int remove=0)=>Box(154,b=>{b.Write((ushort)seconds);b.Write((ushort)key);b.Write((ushort)0);b.Write((ushort)remove);});
    static byte[] Native(int gfx,int seconds)=>Box(154,b=>{b.Write((ushort)seconds);b.Write((uint)gfx);});
    static byte[] Snapshot(byte[] data)=>Box(20,b=>{b.Write((uint)data.Length);b.Write(data);});
    static WorldSession World(){var w=new WorldSession();typeof(WorldSession).GetField("MagicStatusClock",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,(Func<long>)(()=>now));return w;}
    static void Feed(WorldSession w,byte[] p)=>w.Receive(p,"");
    static void Reject(WorldSession w,byte[] p){var before=w.MagicBuffs;bool rejected=false;try{Feed(w,p);}catch(ProtocolException){rejected=true;}Check(rejected,"malformed packet accepted: "+Convert.ToHexString(p));Check(before.SequenceEqual(w.MagicBuffs),"malformed packet partially changed state");}
    [STAThread] static int Main(string[] args){
        try {
            var w=World();
            foreach(var pair in new[]{(5,3),(3,99),(6,151),(7,159),(10,168)}){
                Feed(w,Shield(pair.Item1,300));Check(w.MagicBuffs.Single(x=>x.Skill==pair.Item2).Seconds==300,"shield mapping");
            }
            Check(w.MagicBuffs.Length==5,"shield coexistence");Feed(w,Shield(6,0));Check(w.MagicBuffs.All(x=>x.Skill!=151),"shield removal");
            w=World();
            int[] expected=[148,149,155,156,163,166,3,99,151,159,168,26,110,42,109,114,115,8,48,12];
            for(int i=0;i<expected.Length;i++){
                Feed(w,Proxy(518+i,65535));Check(w.MagicBuffs.Single(x=>x.Skill==expected[i]).Seconds==65535,"proxy mapping and ushort duration");
            }
            Check(w.MagicBuffs.Length==20,"proxy coexistence");
            Feed(w,Native(2165,0));Feed(w,Native(2176,0));Check(w.MagicBuffs.Length==20,"native cleanup erased proxy");
            Feed(w,Proxy(535,100,1));Check(w.MagicBuffs.All(x=>x.Skill!=8),"explicit remove with nonzero duration");
            Feed(w,Proxy(535,2));now+=1999;Check(w.MagicBuffs.Single(x=>x.Skill==8).Remaining(now)==1,"last partial second");
            Feed(w,Proxy(535,10));now+=2000;Check(w.MagicBuffs.Single(x=>x.Skill==8).Remaining(now)==8,"refresh replaces timer");
            now+=8000;Check(w.MagicBuffs.All(x=>x.Skill!=8),"exact expiry removes without new packet");
            foreach(var pair in new[]{(113,114),(114,115),(116,117),(147,148),(148,149),(154,155),(155,156),(162,163),(165,166)}){
                Feed(w,Aura(pair.Item1,60));Check(w.MagicBuffs.Single(x=>x.Skill==pair.Item2).Source=="aura","aura mapping");
                Feed(w,Aura(pair.Item1,0));Check(w.MagicBuffs.All(x=>x.Skill!=pair.Item2),"aura clear");
            }
            w=World();foreach(var pair in new[]{(2165,8),(747,12),(748,21),(2176,48),(2951,107)}){
                Feed(w,Native(pair.Item1,123));Check(w.MagicBuffs.Single(x=>x.Skill==pair.Item2).Seconds==123,"native weapon mapping");
            }
            Feed(w,Proxy(60000,4));Check(w.MagicBuffs.Any(x=>x.Name=="技能圖示 60000"),"unknown proxy has stable neutral label");
            Feed(w,Proxy(60000,0));Check(w.MagicBuffs.All(x=>x.Name!="技能圖示 60000"),"unknown proxy removable");
            w=World();foreach(int sub in new[]{20,21}){
                Feed(w,Box(sub,b=>{b.Write((ushort)1234);b.Write((byte)88);}));
                Check(w.MagicBuffs.Any(x=>x.Name==$"狀態 {sub}/88"&&x.Seconds==1234),"compact H,C format");
                Feed(w,Box(sub,b=>{b.Write((ushort)0);b.Write((byte)88);}));
            }
            Check(w.MagicBuffs.Length==0,"compact clear");
            var data=new byte[204];data[0]=15;data[8]=2;data[9]=3;data[23]=4;data[52]=5;data[10]=20;data[11]=147;data[72]=255;
            Feed(w,Snapshot(data));Check(w.MagicBuffs.Length==6,"snapshot known slots only, ignores epoch bytes");
            Check(w.MagicBuffs.Single(x=>x.Skill==32).Seconds==60,"snapshot four-second units");
            Check(w.MagicBuffs.Single(x=>x.Skill==176).Seconds==64&&w.MagicBuffs.Single(x=>x.Skill==206).Seconds==80,"snapshot sixteen-second units");
            Check(w.MagicBuffs.Single(x=>x.Skill==47) is {Debuff:true,Approximate:true},"lossy debuff duration explicitly approximate");
            Check(w.MagicBuffs.Single(x=>x.Skill==148).Seconds==80,"snapshot elemental identity");
            Feed(w,Proxy(518,90));Feed(w,Snapshot(new byte[204]));Check(w.MagicBuffs.Length==1&&w.MagicBuffs[0].Source=="proxy","snapshot zeros preserve proxy source");
            foreach(int sub in new[]{34,35,36,40}){Feed(w,Box(sub,b=>b.Write((ushort)60)));Check(w.MagicBuffs.Any(x=>x.Source==$"packetbox{sub}"),"simple duration");Feed(w,Box(sub,b=>b.Write((ushort)0)));}
            Feed(w,Box(57,b=>{b.Write((byte)44);b.Write((ushort)90);}));Check(w.MagicBuffs.Any(x=>x.Name=="慎重藥水"&&x.Seconds==90),"wisdom potion");
            typeof(WorldSession).GetProperty("Status")!.SetValue(w,new OwnStatus(123,1,0,1,1,1,1,1,1,10,10,10,10,0,0,0,0,0,0,0,0,0,0));
            Feed(w,Box(44,b=>{b.Write(456u);b.Write((ushort)10);}));Check(w.MagicBuffs.All(x=>x.Skill!=167),"other actor shackle ignored");
            Feed(w,Box(44,b=>{b.Write(123u);b.Write((ushort)10);}));Check(w.MagicBuffs.Single(x=>x.Skill==167) is {Seconds:40,Debuff:true},"own actor shackle units");
            foreach(var valid in new[]{Shield(5,60),Aura(116,60),Proxy(535,60),Native(747,60),Snapshot(data),Box(57,b=>{b.Write((byte)44);b.Write((ushort)90);})}){
                for(int length=0;length<valid.Length;length++){
                    // An 8-byte prefix of S_IconConfig is also a complete native-gfx envelope.
                    if(length==8&&valid[0]==250&&valid[1]==154){var before=w.MagicBuffs;Feed(w,valid[..length]);Check(before.SequenceEqual(w.MagicBuffs),"unknown native gfx must not change buffs");}
                    else Reject(w,valid[..length]);
                }
                if(valid.Length!=8||valid[0]==216||valid[1]!=154){var bad=(byte[])valid.Clone();bad[^1]=255;Reject(w,bad);}
            }
            var badProxy=Proxy(535,60);badProxy[6]=1;Reject(w,badProxy);badProxy=Proxy(535,60,2);Reject(w,badProxy);
            var badSnapshot=Snapshot(data);badSnapshot[2]=203;Reject(w,badSnapshot);
            Reject(w,Box(57,b=>{b.Write((byte)43);b.Write((ushort)60);}));
            now+=90000;Check(w.MagicBuffs.Length==0&&w.MagicBuffNotice=="","expiry removes all notices");
            Feed(w,Shield(5,60));w.Reset();Check(w.MagicBuffs.Length==0,"session reset clears buffs");
            using var icons=new StatusIcons{Font=new Font("Microsoft JhengHei UI",9)};
            using var notice=new Label();icons.UpdateState([new(3,"保護罩",123,false,false),new(47,"弱化術",8,true,true),new(168,"鋼鐵防護",90,false,false)]);
            foreach(int width in new[]{300,600,1000}){icons.ArrangeBeside(notice,new Rectangle(10,0,width,23));Check(notice.Right<icons.Left&&icons.Right<=width+10,"HUD layout bounds");using var bitmap=new Bitmap(icons.Width,icons.Height);icons.DrawToBitmap(bitmap,icons.ClientRectangle);if(args.Length>0&&width==1000){Directory.CreateDirectory(args[0]);bitmap.Save(Path.Combine(args[0],"status-icons.png"));}}
            Check(icons.AccessibleName!.Contains("弱化術 · 約8 秒"),"accessible complete duration list");
            icons.UpdateState([]);icons.ArrangeBeside(notice,new Rectangle(10,0,600,23));Check(!icons.Visible&&notice.Width==600,"empty icons restore notice width");
            Console.WriteLine($"PASS {checks} assertions; synthetic packets and isolated HUD control only; no game/network/assets/database.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
