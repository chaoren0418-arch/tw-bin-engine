using P06;
using System.Buffers.Binary;
using System.Drawing;

// T168 (#440): names, titles, emblems and name colours, docs/reference/actions-and-names.md 2.2-2.8.
// Every server packet goes through WorldSession.Receive; every C72 goes through the product send gate.
partial class Checks {
    static byte[] NamePack(uint id,ushort gfx=1110,short lawful=0,string name="target",string title="",uint clanId=0,string clan="")=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(id);b.Write(gfx);b.Write((byte)0);b.Write(new byte[3]);b.Write(0u);b.Write(lawful);
        Text(b,name);Text(b,title);b.Write((byte)0);b.Write(clanId);Text(b,clan);Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] OwnNamePack(short lawful=0,uint clanId=0,string clan="",string title="")=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(123u);b.Write((ushort)0);b.Write((byte)0);b.Write(new byte[3]);b.Write(0u);b.Write(lawful);
        Text(b,"offline");Text(b,title);b.Write((byte)0);b.Write(clanId);Text(b,clan);Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] LawfulPacket(uint id,short lawful)=>Packet(b=>{b.Write((byte)34);b.Write(id);b.Write(lawful);});
    static byte[] RawEmblemPacket(uint id,Func<int,ushort> pixel)=>Packet(b=>{b.Write((byte)118);b.Write(id);for(int i=0;i<192;i++)b.Write(pixel(i));});
    static void NameReject(Action action,string label){bool failed=false;try{action();}catch(ProtocolException){failed=true;}Check(failed,label);}

    [CheckGroup("name-display"),CheckGroup("map-continuity",Order=40),CheckGroup(CheckGroups.Default,Order=280)]
    static async Task NameDisplayChecks(){
        string scratch=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","scratch","name-display-tests"));Directory.CreateDirectory(scratch);
        string disk=Path.Combine(scratch,"emblems.bin"),options=Path.Combine(scratch,"name-display.txt");
        string? oldDisk=ClanEmblemCache.DiskPath,oldOptions=NameDisplayOptions.FilePath;
        try{
            NameColourChecks();
            NameLayoutChecks();
            NamePlanChecks();
            await EmblemRequestChecks();
            EmblemCacheChecks(disk);
            NameOptionChecks(options);
        }finally{ClanEmblemCache.DiskPath=oldDisk;NameDisplayOptions.FilePath=oldOptions;}
    }

    static void NameColourChecks(){
        // 2.4 sub_A406C0, every branch and both clamps.
        Check(NameColors.Lawful(0)==0x7FFF&&NameColors.Lawful(499)==0x7FFF,"lawful 0..499 white");
        Check(NameColors.Lawful(500)==NameColors.Rgb(30,30,31)&&NameColors.Lawful(755)==NameColors.Rgb(30,30,31)&&NameColors.Lawful(756)==NameColors.Rgb(29,29,31),"lawful >=500 v=((L-500)>>8)+1");
        Check(NameColors.Lawful(32767)==0x001F,"lawful blue clamps at v=31");
        Check(NameColors.Lawful(-1)==NameColors.Rgb(31,30,30)&&NameColors.Lawful(-63)==NameColors.Rgb(31,30,30)&&NameColors.Lawful(-64)==NameColors.Rgb(31,29,29),"lawful <0 v=((-L)>>6)+1");
        Check(NameColors.Lawful(-32768)==0x7C00,"lawful red clamps at v=31");
        Check(NameColors.ToColor(0x767D)==Color.FromArgb(238,156,238)&&NameColors.ToColor(0)==Color.FromArgb(0,0,0),"RGB555 to screen colour");

        using(var c=Seed()){
            Feed(c,NamePack(456,lawful:-640));Check(c.World.NameColor555(456)==NameColors.Lawful(-640),"S87 lawful colours another character");
            // S34 S_Lawful(pc): D H, both own and others.
            Feed(c,LawfulPacket(456,1000));Check(c.World.Objects.Single(o=>o.ObjectId==456).Lawful==1000&&c.World.NameColor555(456)==NameColors.Lawful(1000),"S34 D H updates other lawful colour");
            Feed(c,LawfulPacket(123,-5000));Check(c.World.Actor!.Lawful==-5000&&c.World.Status!.Lawful==-5000&&c.World.NameColor555(123)==NameColors.Lawful(-5000),"S34 own lawful updates actor and status");
            Feed(c,Packet(b=>{b.Write((byte)34);b.Write(456u);b.Write((short)-32768);b.Write((short)-32768);b.Write((short)-32768);}));Check(c.World.NameColor555(456)==0x7C00,"S34 S_Lawful(objid) three-H form");
            NameReject(()=>Feed(c,Packet(b=>{b.Write((byte)34);b.Write(456u);b.Write((short)5);b.Write((short)1);b.Write((short)2);})),"S34 unknown trailer rejected");
            var bytes=LawfulPacket(456,77);for(int n=1;n<7;n++)NameReject(()=>Feed(c,bytes[..n]),"S34 truncated "+n);
            var bad=(byte[])bytes.Clone();bad[^1]=1;NameReject(()=>Feed(c,bad),"S34 nonzero padding");
            Check(c.World.NameColor555(456)==0x7C00,"rejected S34 changes nothing");
            int unknown=c.World.UnknownObjectUpdates;Feed(c,LawfulPacket(999,10));Check(c.World.UnknownObjectUpdates==unknown+1,"S34 unknown object does not spawn");

            // 2.5 pink: colour 0x767D, timer, 127 permanent, 0 immediate clear, then lawful colour again.
            long before=Environment.TickCount64;Feed(c,PinkPacket(456,3));
            Check(c.World.NameColor555(456)==NameColors.Pink,"S60 pink name colour 0x767D");
            Check(c.World.NameColor555(456,before+10000)==0x7C00,"pink expiry restores lawful colour");
            Feed(c,PinkPacket(456,127));Check(c.World.NameColor555(456,long.MaxValue-1)==NameColors.Pink,"S60 127 never expires");
            Feed(c,PinkPacket(456,0));Check(c.World.NameColor555(456)==0x7C00,"S60 0 clears at once");
            Feed(c,PinkPacket(123,127));Check(c.World.NameColor555(123)==NameColors.Pink,"own pink name colour");Feed(c,PinkPacket(123,0));
            Feed(c,LoginPackets.Pad([206,4,0,0]));Feed(c,NamePack(456));Feed(c,PinkPacket(456,127));Feed(c,LoginPackets.Pad([206,0,0,0]));Feed(c,NamePack(456));
            Check(c.World.NameColor555(456)==0x7FFF,"permanent pink ends with the world");
        }
        using(var c=Seed()){
            // gfx 2715 is red outside war mode only.
            Feed(c,NamePack(456,gfx:2715,lawful:1000));Check(c.World.NameColor555(456)==NameColors.Red,"gfx 2715 red outside war");
            Feed(c,ClanUpdatePacket(123,"甲盟"));Feed(c,ClanUpdatePacket(456,"乙盟"));Feed(c,WarPacket(1,"甲盟","乙盟"));
            Check(c.World.ClanWarMode&&c.World.NameColor555(456)==NameColors.Lawful(1000),"gfx 2715 uses lawful colour in war mode");
        }
        using(var c=Seed()){
            // 2.6/2.7 duel opponent and enemy clan: no colour change, only direct attack.
            Feed(c,NamePack(456,lawful:600));ushort normal=c.World.NameColor555(456);
            Check(!c.World.CanAttackDirectly(456),"plain character is not a direct attack target");
            Feed(c,DuelState());Check(c.World.IsDuelOpponent(456)&&c.World.NameColor555(456)==normal&&c.World.CanAttackDirectly(456),"duel opponent keeps name colour, may be attacked directly");
            Feed(c,DuelState(0,0));Check(!c.World.CanAttackDirectly(456),"duel end removes direct attack");
            Feed(c,ClanUpdatePacket(123,"甲盟"));Feed(c,ClanUpdatePacket(456,"乙盟"));
            Check(c.World.ClanWarFlag(456)==0&&c.World.ClanWarFlag(123)==0,"no war flags outside war mode");
            Feed(c,WarPacket(1,"甲盟","乙盟"));
            Check(c.World.ClanWarFlag(456)==2&&c.World.ClanWarFlag(123)==1,"war mode +197: 2 enemy, 1 own clan");
            Check(c.World.NameColor555(456)==normal&&c.World.CanAttackDirectly(456)&&!c.World.CanAttackDirectly(123),"enemy clan keeps colour, direct attack; never self");
            Feed(c,WarPacket(3,"甲盟","乙盟"));Check(!c.World.CanAttackDirectly(456)&&c.World.ClanWarFlag(456)==0,"war end clears enemy flag");
            Feed(c,PinkPacket(456,127));Check(c.World.CanAttackDirectly(456),"pink name may be attacked directly");
        }
    }

    static void NameLayoutChecks(){
        var r=new Rectangle(100,200,48,80);
        Check(NameLayout.ByteLength("ab中")==4&&NameLayout.CenterX(r,"測試")==112&&NameLayout.CenterX(r,"abc")==115,"X0 = L + (W - 6*Big5 bytes)/2");
        var plain=NameLayout.Hover(r,"","測試",false,false);
        Check(plain.Title==null&&plain.Name==new PointF(112,197),"hover name at T-3 without title");
        var clan=NameLayout.Hover(r,"","測試",true,false);
        Check(clan.Name==new PointF(122,197)&&clan.Emblem==new PointF(92,196),"clan id shifts name +10, emblem at X0-20, T-4");
        var titled=NameLayout.Hover(r,"稱號甲","測試",true,false);
        Check(titled.Title==new PointF(106,197)&&titled.Name==new PointF(122,210)&&titled.Emblem==new PointF(92,209),"title one line above: title T-3, name T+10, emblem +13");
        var party=NameLayout.Hover(r,"","測試",true,true);
        Check(party.Name==new PointF(122,188)&&party.Emblem==new PointF(92,187),"party member other than self -9");
        Check(NameLayout.HeadEmblem(r)==new PointF(116,177),"head emblem centred, T-23");
        var all=NameLayout.AllNames(r,"測試",true);Check(all.Name==new PointF(122,184)&&all.Emblem==new PointF(92,183),"all-names mode name at T-16");
    }

    static void NamePlanChecks(){
        using var c=Seed();
        Feed(c,OwnNamePack(title:"自身稱號"));Feed(c,NamePack(456,lawful:-640,title:"遠方稱號"));Feed(c,NamePack(789,name:"第三人"));
        var targets=new List<NameTarget>{new(123,"offline",new(100,100,48,80),true),new(456,"target",new(300,100,48,80),false),new(789,"第三人",new(500,100,48,80),false)};
        var none=NameLayout.Plan(c.World,targets,null,false);Check(none.Texts.Length==0&&none.Emblems.Length==0,"nothing drawn without hover or option");
        var hover=NameLayout.Plan(c.World,targets,456,false);
        Check(hover.Texts.Length==2&&hover.Texts.All(t=>t.ObjectId==456),"only the hovered object is named");
        Check(hover.Texts[0] is {Title:true,Text:"遠方稱號"}&&hover.Texts[1] is {Title:false,Text:"target"},"title then name");
        Check(hover.Texts.All(t=>t.Color==NameColors.Lawful(-640)),"title uses the name colour");
        Check(hover.Texts[0].Position.Y+NameLayout.TitleLine==hover.Texts[1].Position.Y,"title is one line above the name");
        var self=NameLayout.Plan(c.World,targets,123,false);Check(self.Texts.Select(t=>t.Text).SequenceEqual(new[]{"自身稱號","offline"}),"hovering self names self");
        var all=NameLayout.Plan(c.World,targets,456,true);
        Check(all.Texts.Count(t=>!t.Title)==3&&all.Texts.Count(t=>t.Title)==1&&all.Texts.Single(t=>t.Title).ObjectId==456,"all-names option: every name, title only for hovered");
        Check(all.Texts.Single(t=>t.ObjectId==789).Position==new PointF(NameLayout.CenterX(targets[2].Bounds,"第三人"),100-16),"all-names placement");
        // Clan names and duel captions are never drawn as text.
        Feed(c,ClanUpdatePacket(456,"乙盟"));Feed(c,DuelState());
        Check(NameLayout.Plan(c.World,targets,456,true).Texts.All(t=>!t.Text.Contains("乙盟")&&!t.Text.Contains("決鬥")),"no [clan] or duel label text");
        Feed(c,Blind(1));var blind=NameLayout.Plan(c.World,targets,456,true);
        Check(blind.Texts.All(t=>t.ObjectId==123),"blind mode 1 hides other names");Feed(c,Blind(0));

        // Emblems: beside the name for the clan id, head emblem for own clan members and war flags.
        using var w=Seed();
        Feed(w,OwnNamePack(clanId:7,clan:"甲盟"));Feed(w,NamePack(456,clanId:8,clan:"乙盟"));Feed(w,NamePack(789,name:"第三人",clanId:7,clan:"甲盟"));
        var plan=NameLayout.Plan(w.World,targets,456,false);
        Check(plan.Emblems.Any(e=>e is {ObjectId:456,EmblemId:8,OverHead:false}&&e.Position==NameLayout.Hover(targets[1].Bounds,"","target",true,false).Emblem),"hovered emblem left of the name");
        Check(plan.Texts.Single(t=>!t.Title).Position.X==NameLayout.CenterX(targets[1].Bounds,"target")+10,"hovered name shifted by the emblem");
        Check(plan.Emblems.Where(e=>e.OverHead).Select(e=>e.ObjectId).Order().SequenceEqual(new uint[]{123,789}),"own clan members carry a head emblem");
        Check(plan.Emblems.Single(e=>e.OverHead&&e.ObjectId==789).Position==NameLayout.HeadEmblem(targets[2].Bounds),"head emblem position");
        Feed(w,WarPacket(1,"甲盟","乙盟"));var war=NameLayout.Plan(w.World,targets,null,true);
        Check(war.Emblems.Where(e=>e.OverHead).Select(e=>e.ObjectId).Order().SequenceEqual(new uint[]{123,456,789}),"war mode adds enemy head emblems");
        Check(war.Texts.Length==0,"all-names mode skips +196 (war) characters");
    }

    static async Task EmblemRequestChecks(){
        ClanEmblemCache.DiskPath=null;
        // TW16 384-byte RGB555 payload; 0 is transparent.
        var raw=ClanProtocol.DecodeEmblem(RawEmblemPacket(55,i=>(ushort)(i==0?0:0x7C00)));
        Check(raw.Id==55&&raw.Pixels[0]==0&&raw.Pixels[1]==unchecked((int)0xffff0000),"S118 raw 16x12 RGB555 with transparent 0");
        NameReject(()=>ClanProtocol.DecodeEmblem(RawEmblemPacket(55,_=>1)[..300]),"S118 raw truncated");
        using var c=Seed();var sent=new List<byte[]>();bool admit=true;
        Sender(c,(b,v,a)=>{if(!admit||!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        Check(c.RequestClanEmblem(77)&&sent.Count==1&&sent[0].SequenceEqual(ClanProtocol.Encode(ClanCommand.Emblem,emblemId:77))&&sent[0].SequenceEqual(Convert.FromHexString("484D000000000000")),"missing emblem sends C72 D(id)");
        Check(!c.RequestClanEmblem(77)&&sent.Count==1,"pending id is never sent again");
        Check(!c.RequestClanEmblem(0)&&sent.Count==1,"clan id 0 has no emblem");
        for(uint id=1000;id<1040;id++)c.RequestClanEmblem(id);
        Check(sent.Count==ClanEmblemCache.MaxPending&&c.World.Emblems.PendingCount==32,"at most 32 replies outstanding");
        Feed(c,RawEmblemPacket(77,_=>0x001F));Check(c.World.FindClanEmblem(77) is {} e&&e.Pixels.All(p=>p==unchecked((int)0xff0000ff))&&c.World.Emblems.PendingCount==31,"S118 reply fills cache and frees a slot");
        Check(!c.RequestClanEmblem(77),"cached emblem is not requested");
        Check(c.RequestClanEmblem(2000)&&sent.Count==33,"freed slot admits the next missing id");
        Feed(c,LoginPackets.Pad([118,0xD0,7,0,0,1]));Check(c.World.Emblems.PendingCount==31&&!c.RequestClanEmblem(2000)&&sent.Count==33,"unreadable reply is final, no resend");
        admit=false;Feed(c,RawEmblemPacket(1000,_=>1));c.RequestClanEmblem(3000);Check(sent.Count==33&&c.World.Emblems.PendingCount==30,"send gate refusal releases the slot");
        admit=true;Check(c.RequestClanEmblem(3000)&&sent.Count==34,"released id can be sent once admitted");
        Feed(c,LoginPackets.Pad([206,4,0,0]));Check(c.World.FindClanEmblem(77)!=null,"emblem cache survives a map change");
        c.World.Reset();Check(c.World.FindClanEmblem(77)!=null,"emblem cache survives a world reset (original keeps a cross-connection cache)");
        Set(c,"generation",(int)typeof(LoginConnection).GetField("generation",F)!.GetValue(c)!+1);
        Check(c.RequestClanEmblem(1001)&&c.World.Emblems.PendingCount==1,"new connection frees the old outstanding slots");
        await Task.CompletedTask;
        using var f=Seed();Sender(f,(b,v,a)=>throw new IOException("synthetic"));
        Check(f.RequestClanEmblem(5)&&f.State==LoginState.Faulted,"C72 transport failure disconnects without retry");
    }

    static void EmblemCacheChecks(string disk){
        if(File.Exists(disk))File.Delete(disk);ClanEmblemCache.DiskPath=null;
        var cache=new ClanEmblemCache();ClanEmblem Make(uint id)=>new(id,Enumerable.Repeat(unchecked((int)(0xff000000|id)),192).ToArray());
        for(uint id=1;id<=512;id++)cache.Store(Make(id));Check(cache.Count==512,"memory cache holds 512");
        Check(cache.Find(1)!=null,"touch oldest");cache.Store(Make(513));
        Check(cache.Count==512&&cache.Find(1)!=null&&cache.Find(2)==null&&cache.Find(513)!=null,"513th replaces the least recently used");
        var copy=cache.Find(5)!;copy.Pixels[0]=0;Check(cache.Find(5)!.Pixels[0]!=0,"snapshots cannot mutate cache");

        ClanEmblemCache.DiskPath=disk;
        using(var c=Seed()){Feed(c,RawEmblemPacket(321,_=>0x03E0));Feed(c,RawEmblemPacket(322,_=>0x7FFF));}
        Check(File.Exists(disk)&&Path.GetDirectoryName(disk)!.Contains("scratch"),"disk cache is written to the configured user-data path");
        using(var c=Seed()){
            Check(c.World.FindClanEmblem(321) is {} e&&e.Pixels[0]==unchecked((int)0xff00ff00)&&c.World.FindClanEmblem(322)!=null,"next connection loads the disk cache");
            var sent=0;Sender(c,(b,v,a)=>{sent++;a();return Task.FromResult(true);});Check(!c.RequestClanEmblem(321)&&sent==0,"disk-cached emblem is not requested");
        }
        File.WriteAllBytes(disk,[1,2,3]);
        using(var c=Seed())Check(c.World.FindClanEmblem(321)==null,"corrupt disk cache is ignored");
        File.Delete(disk);ClanEmblemCache.DiskPath=null;
        Check(ClanEmblemCache.DefaultDiskPath.EndsWith(Path.Combine("runtime","user","emblems.bin"))&&!ClanEmblemCache.DefaultDiskPath.Contains("0-客戶端"),"default disk cache is the engine user-data directory, not the client");
    }

    static void NameOptionChecks(string options){
        if(File.Exists(options))File.Delete(options);NameDisplayOptions.FilePath=options;
        Check(!NameDisplayOptions.LoadShowAll(),"show-all-names defaults off");
        NameDisplayOptions.SaveShowAll(true);Check(NameDisplayOptions.LoadShowAll(),"show-all-names persists on");
        NameDisplayOptions.SaveShowAll(false);Check(!NameDisplayOptions.LoadShowAll(),"show-all-names persists off");
        File.Delete(options);
    }
}
