using P06;
using System.Drawing;

partial class Checks {
    static long timedNow=50000;
    static byte[] Strup(int seconds,byte strength,byte weight,byte type)=>Packet(b=>{b.Write((byte)166);b.Write((ushort)seconds);b.Write(strength);b.Write(weight);b.Write(type);});
    static byte[] Dexup(int seconds,byte dexterity,byte type)=>Packet(b=>{b.Write((byte)188);b.Write((ushort)seconds);b.Write(dexterity);b.Write(type);});
    static byte[] Liquor(uint id,byte type)=>Packet(b=>{b.Write((byte)103);b.Write(id);b.Write(type);});
    static byte[] Eva(uint id,int seconds)=>Packet(b=>{b.Write((byte)126);b.Write(id);b.Write((ushort)seconds);});
    static byte[] SkillMasks(byte opcode,byte tier,int groups,params (int Row,byte Mask)[] masks)=>Packet(b=>{
        var rows=new byte[groups];foreach(var m in masks)rows[m.Row]=m.Mask;
        b.Write(opcode);b.Write(tier);b.Write(rows);
    });
    static byte[] Haste(uint id,byte mode,int seconds)=>Packet(b=>{b.Write((byte)255);b.Write(id);b.Write(mode);b.Write((ushort)seconds);});
    static byte[] MapChange(ushort map)=>Packet(b=>{b.Write((byte)206);b.Write(map);b.Write((byte)0);});
    static WorldSession TimedWorld(){
        var w=new WorldSession();w.CacheEnabled=false;Set(w,"MagicStatusClock",(Func<long>)(()=>timedNow));
        Set(w,"Status",new OwnStatus(123,1,0,10,11,12,13,14,15,100,100,100,100,0,0,0,50,0,0,0,0,0,0));
        PolyFeed(w,PolyPack(123,0,0,0,0));return w;
    }
    static int Unhandled(WorldSession w,byte opcode)=>((System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(w)!).GetValueOrDefault(opcode);
    static MagicBuff? Timed(WorldSession w,int skill)=>w.MagicBuffs.FirstOrDefault(b=>b.Skill==skill);
    static void RejectTimed(WorldSession w,byte[] bytes,string label){
        var status=w.Status;var buffs=w.MagicBuffs;var learned=w.LearnedMagic.Select(m=>m.Id).ToArray();int level=w.LiquorLevel;bool rejected=false;
        try{PolyFeed(w,bytes);}catch(ProtocolException){rejected=true;}
        Check(rejected,"TIMED rejects "+label);
        Check(w.Status==status&&buffs.SequenceEqual(w.MagicBuffs)&&learned.SequenceEqual(w.LearnedMagic.Select(m=>m.Id))&&w.LiquorLevel==level,"TIMED rejection is atomic: "+label);
    }
    static void RejectMalformed(WorldSession w,byte[] valid,string label){
        for(int length=1;length<valid.Length;length++)RejectTimed(w,valid[..length],$"{label} truncated to {length}");
        var bad=(byte[])valid.Clone();bad[^1]=1;RejectTimed(w,bad,label+" nonzero padding");
        RejectTimed(w,[..valid,..new byte[8]],label+" extra frame");
    }
    static void TimedBuffChecks(){
        TimedAbilityChecks();TimedEvaChecks();TimedLiquorChecks();TimedDelSkillChecks();TimedShakeMountChecks();
    }
    static void TimedAbilityChecks(){
        // Byte layout from the capture comment in S_Strup.java, with the opcode and zero padding of this build.
        var w=TimedWorld();PolyFeed(w,Convert.FromHexString("A6B004105A050000"));
        Check(w.Status is {Strength:16,Weight:90,Dexterity:13},"TIMED S166 HCCC updates strength and weight only");
        Check(Timed(w,42) is {Seconds:1200,Name:"力量提升",Source:"strup",Debuff:false,Approximate:false},"TIMED S166 exact timer");
        timedNow+=2500;Check(Timed(w,42)!.Remaining(timedNow)==1198,"TIMED S166 remaining time counts down");
        PolyFeed(w,Strup(600,16,90,5));timedNow+=1000;Check(Timed(w,42)!.Remaining(timedNow)==599,"TIMED S166 refresh replaces timer");
        Check(w.MagicBuffNotice.Contains("力量提升：剩餘 599 秒"),"TIMED S166 reaches the existing notice");
        PolyFeed(w,Strup(0,11,80,1));Check(Timed(w,42)==null&&w.Status is {Strength:11,Weight:80},"TIMED S166 stop removes timer and restores value");
        PolyFeed(w,Strup(300,13,70,2));Check(Timed(w,109)!.Seconds==300&&Timed(w,42)==null,"TIMED S166 type 2 uses dress slot");
        PolyFeed(w,Strup(120,18,70,5));Check(Timed(w,109)!=null&&Timed(w,42)!=null,"TIMED S166 both sources coexist");
        PolyFeed(w,Strup(0,16,70,2));Check(Timed(w,109)==null&&Timed(w,42)!=null,"TIMED S166 stop leaves the other source");
        PolyFeed(w,Strup(0,11,70,1));Check(w.MagicBuffs.Length==0,"TIMED S166 last stop empties list");
        PolyFeed(w,Strup(60,12,70,2));PolyFeed(w,Strup(0,10,70,1));Check(w.MagicBuffs.Length==0,"TIMED S166 enchant configured to 2 stops with type 1");
        PolyFeed(w,Strup(3,12,70,5));timedNow+=3000;Check(Timed(w,42)==null,"TIMED S166 exact expiry without a packet");
        PolyFeed(w,Strup(65535,255,240,5));Check(Timed(w,42)!.Seconds==65535&&w.Status is {Strength:255,Weight:240},"TIMED S166 unsigned fields");
        PolyFeed(w,Strup(0,11,70,1));RejectMalformed(w,Strup(60,12,70,5),"S166");

        w=TimedWorld();PolyFeed(w,Dexup(1200,18,5));
        Check(w.Status is {Dexterity:18,Strength:10,Weight:50},"TIMED S188 HCC updates dexterity only");
        Check(Timed(w,26) is {Seconds:1200,Name:"敏捷提升",Source:"dexup"},"TIMED S188 exact timer");
        timedNow+=1000;Check(Timed(w,26)!.Remaining(timedNow)==1199,"TIMED S188 remaining time counts down");
        PolyFeed(w,Dexup(0,13,1));Check(Timed(w,26)==null&&w.Status!.Dexterity==13,"TIMED S188 stop");
        PolyFeed(w,Dexup(300,15,2));Check(Timed(w,110)!.Seconds==300,"TIMED S188 type 2 uses dress slot");
        PolyFeed(w,Dexup(0,13,2));Check(w.MagicBuffs.Length==0,"TIMED S188 dress stop");
        PolyFeed(w,Dexup(60,15,2));PolyFeed(w,Dexup(0,13,1));Check(w.MagicBuffs.Length==0,"TIMED S188 enchant configured to 2 stops with type 1");
        RejectMalformed(w,Dexup(60,15,5),"S188");

        var early=new WorldSession();Set(early,"MagicStatusClock",(Func<long>)(()=>timedNow));
        PolyFeed(early,Strup(60,16,90,5));PolyFeed(early,Dexup(60,18,5));
        Check(early.Status==null&&early.MagicBuffs.Length==2,"TIMED S166/S188 before S8 keep the timer and do not fault the session");
        w=TimedWorld();PolyFeed(w,Strup(60,16,90,5));w.Reset();Check(w.MagicBuffs.Length==0,"TIMED ability timers end with the session");
    }
    static void TimedEvaChecks(){
        var w=TimedWorld();PolyFeed(w,Eva(123,1800));
        Check(Timed(w,TimedBuffRules.WaterBreath) is {Seconds:1800,Name:"水中呼吸",Source:"bless-of-eva",Debuff:false},"TIMED S126 DH own timer");
        timedNow+=1000;Check(Timed(w,TimedBuffRules.WaterBreath)!.Remaining(timedNow)==1799,"TIMED S126 remaining time counts down");
        PolyFeed(w,Eva(123,3600));Check(Timed(w,TimedBuffRules.WaterBreath)!.Seconds==3600,"TIMED S126 item stacking refreshes");
        PolyFeed(w,Eva(456,0));Check(Timed(w,TimedBuffRules.WaterBreath)!=null,"TIMED S126 other object ignored");
        PolyFeed(w,Eva(123,0));Check(Timed(w,TimedBuffRules.WaterBreath)==null,"TIMED S126 zero clears");
        PolyFeed(w,Eva(123,2));timedNow+=2000;Check(Timed(w,TimedBuffRules.WaterBreath)==null,"TIMED S126 exact expiry");
        RejectMalformed(w,Eva(123,60),"S126");
        var early=new WorldSession();Set(early,"MagicStatusClock",(Func<long>)(()=>timedNow));PolyFeed(early,Eva(123,60));
        Check(early.MagicBuffs.Length==1,"TIMED S126 is sent only to its owner, so unknown identity keeps it");
    }
    static void TimedLiquorChecks(){
        var w=TimedWorld();Check(w.LiquorLevel==0&&w.LiquorShake.IsEmpty,"TIMED S103 sober has no shake");
        PolyFeed(w,Liquor(123,1));Check(w.LiquorLevel==1,"TIMED S103 DC own level");
        var seen=new HashSet<Point>();for(int ms=0;ms<3400;ms+=50){timedNow+=50;seen.Add(w.LiquorShake);}
        Check(seen.Count>4&&seen.All(p=>Math.Abs(p.X)<=2&&Math.Abs(p.Y)<=1),"TIMED S103 level 1 sways within its bound");
        Check(seen.Any(p=>p.X<0)&&seen.Any(p=>p.X>0),"TIMED S103 sways both ways");
        foreach(byte level in new byte[]{2,3}){
            PolyFeed(w,Liquor(123,level));Check(w.LiquorLevel==level,"TIMED S103 level "+level);
            int peak=Enumerable.Range(0,120).Max(i=>Math.Abs(TimedBuffRules.ShakeOffset(level,i*10).X));Check(peak==level*2,"TIMED S103 strength scales "+level);
        }
        PolyFeed(w,Liquor(123,0));Check(w.LiquorLevel==0&&w.LiquorShake.IsEmpty,"TIMED S103 type 0 clears");
        PolyFeed(w,Liquor(456,1));Check(w.LiquorLevel==0,"TIMED S103 other object does not shake own screen");
        PolyFeed(w,Liquor(123,1));PolyFeed(w,Liquor(456,0));Check(w.LiquorLevel==1,"TIMED S103 other object does not clear own screen");
        int before=Unhandled(w,103);PolyFeed(w,Liquor(123,8));
        Check(w.LiquorLevel==1&&Unhandled(w,103)==before+1,"TIMED S103 third-speed type 8 is not drunkenness and stays visible as unhandled");
        PolyFeed(w,Liquor(456,8));Check(w.LiquorLevel==1&&Unhandled(w,103)==before+2,"TIMED S103 type 8 on other objects");

        PolyFeed(w,Haste(456,1,300));Check(w.LiquorLevel==1,"TIMED S103 another object's haste keeps own state");
        PolyFeed(w,Haste(123,0,0));Check(w.LiquorLevel==1,"TIMED S103 haste ending keeps drunkenness");
        PolyFeed(w,Haste(123,2,30));Check(w.LiquorLevel==1,"TIMED S103 slow keeps drunkenness");
        PolyFeed(w,Haste(123,1,300));Check(w.LiquorLevel==0,"TIMED S103 own haste ends drunkenness");
        PolyFeed(w,Liquor(123,1));Check(w.LiquorLevel==1,"TIMED S103 drinking while hasted shakes");
        PolyFeed(w,Haste(123,0,0));Check(w.LiquorLevel==1,"TIMED S103 later haste end leaves new drunkenness");

        PolyFeed(w,MapChange(4));Check(w.LiquorLevel==0,"TIMED S103 S206 clears client state");
        PolyFeed(w,PolyPack(123,0,0,0,0));PolyFeed(w,Liquor(123,1));Check(w.LiquorLevel==1,"TIMED S103 server resend after S206 restores it");
        RejectMalformed(w,Liquor(123,1),"S103");
        w.Reset();Check(w.LiquorLevel==0&&w.LiquorShake.IsEmpty,"TIMED S103 session reset clears");
        Check(TimedBuffRules.ShakeOffset(0,300).IsEmpty&&TimedBuffRules.ShakeOffset(8,300).IsEmpty,"TIMED shake only for levels 1 to 3");
    }
    static void TimedDelSkillChecks(){
        var w=TimedWorld();PolyFeed(w,SkillMasks(164,32,28,(0,0b1001),(1,1)));
        Check(w.LearnedMagic.Select(m=>m.Id).SequenceEqual([1,4,9]),"TIMED S160 fixture learned list");
        Check(w.SelectMagic(4),"TIMED S160 fixture selection");
        int revision=w.MagicRevision;PolyFeed(w,SkillMasks(160,32,32,(0,0b1000)));
        Check(w.LearnedMagic.Select(m=>m.Id).SequenceEqual([1,9]),"TIMED S160 single form removes one skill from the UI list source");
        Check(w.SelectedMagicId==null,"TIMED S160 clears a removed selection");
        Check(w.MagicRevision>revision&&w.MagicNotice=="原服移除技能已更新","TIMED S160 publishes the change");
        Check(!w.SelectMagic(4),"TIMED S160 removed skill cannot be selected");
        Check(w.SelectMagic(9),"TIMED S160 fixture second selection");
        PolyFeed(w,SkillMasks(160,50,28,(0,1)));
        Check(w.LearnedMagic.Select(m=>m.Id).SequenceEqual([9])&&w.SelectedMagicId==9,"TIMED S160 group form keeps other selection");
        PolyFeed(w,SkillMasks(160,100,28,(1,1)));Check(w.LearnedMagic.Length==0,"TIMED S160 tier 100");
        PolyFeed(w,SkillMasks(160,32,32,(31,0x80)));Check(w.LearnedMagic.Length==0,"TIMED S160 unknown skill is harmless");
        foreach(int groups in new[]{28,32}){
            w=TimedWorld();PolyFeed(w,SkillMasks(164,32,28,(0,0b1001)));
            var valid=SkillMasks(160,32,groups,(0,1));
            for(int length=1;length<valid.Length;length++){
                if(length==32)continue; // A 32-byte prefix of the 40-byte form is itself a complete 28-mask frame.
                RejectTimed(w,valid[..length],$"S160/{groups} truncated to {length}");
            }
            var bad=(byte[])valid.Clone();bad[^1]=1;RejectTimed(w,bad,$"S160/{groups} nonzero padding");
            bad=(byte[])valid.Clone();bad[1]=33;RejectTimed(w,bad,$"S160/{groups} unknown tier");
        }
    }
    static void TimedShakeMountChecks(){
        using var bitmap=new Bitmap(16,16);
        using(var graphics=Graphics.FromImage(bitmap)){
            graphics.Clear(Color.Black);Renderer.ApplyScreenShake(graphics,Point.Empty);Check(graphics.Transform.IsIdentity,"TIMED mount leaves a sober frame alone");
            Renderer.ApplyScreenShake(graphics,new(3,-2));graphics.FillRectangle(Brushes.White,5,5,1,1);
        }
        Check(bitmap.GetPixel(8,3).R==255&&bitmap.GetPixel(5,5).R==0,"TIMED mount shifts drawn pixels");
        Check(typeof(ClientForm).GetMethod("CreateHandle",F)!.DeclaringType==typeof(ClientForm)&&typeof(ClientForm).GetMethod("PaintLiquorShake",F)!=null,"TIMED client form registers the paint mount");
        using var probe=new ShakePaintProbe{Size=new(16,16)};probe.Paint+=(_,e)=>Renderer.ApplyScreenShake(e.Graphics,new(3,2));
        using var painted=new Bitmap(16,16);probe.DrawToBitmap(painted,new(0,0,16,16));
        Check(painted.GetPixel(8,7).R==255&&painted.GetPixel(5,5).R==0,"TIMED Paint handler shifts what OnPaint draws after base.OnPaint");
    }
    // Same order as ClientForm.OnPaint: base.OnPaint raises Paint, then the scene is drawn.
    sealed class ShakePaintProbe:Control {
        public ShakePaintProbe(){BackColor=Color.Black;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.FillRectangle(Brushes.White,5,5,1,1);}
    }
}
