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
        var w=TimedWorld();Check(w.LiquorLevel==0,"TIMED S103 sober has no wave");
        PolyFeed(w,Liquor(123,1));Check(w.LiquorLevel==1&&w.LiquorPhase==0,"TIMED S103 DC own level, phase starts at 0");
        timedNow+=49;Check(w.LiquorPhase==0,"TIMED S103 phase holds within a 50 ms tick");
        timedNow+=1;Check(w.LiquorPhase==1,"TIMED S103 phase advances every 50 ms");
        timedNow+=50*199;Check(w.LiquorPhase==0,"TIMED S103 phase wraps at 200 (5 s)");
        timedNow+=50*37;Check(w.LiquorPhase==37,"TIMED S103 phase after wrap");
        foreach(byte level in new byte[]{2,3}){PolyFeed(w,Liquor(123,level));Check(w.LiquorLevel==level&&w.LiquorPhase==0,"TIMED S103 level "+level);}
        PolyFeed(w,Liquor(123,0));Check(w.LiquorLevel==0,"TIMED S103 type 0 clears");
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
        w.Reset();Check(w.LiquorLevel==0,"TIMED S103 session reset clears");
        TimedScanlineTable();
    }
    static void TimedScanlineTable(){
        // AM§4(c): dx(j) = (int)(level*3.5*sin((phase+j)*pi/50)); C-style truncation toward zero.
        (int Level,int Phase,int Row,int Dx)[] table=[
            (1,0,0,0),(1,0,25,3),(1,0,50,0),(1,0,75,-3),(1,0,10,2),(1,0,60,-2),
            (2,0,25,7),(2,0,75,-7),(2,0,5,2),(2,0,55,-2),
            (3,0,25,10),(3,0,75,-10),(3,0,8,5),(3,0,58,-5),
            (1,25,0,3),(3,50,25,-10),(3,199,26,10),(2,13,12,7)];
        foreach(var (level,phase,row,dx) in table)
            Check(TimedBuffRules.ScanlineOffset(level,phase,row)==dx,$"TIMED scanline table level {level} phase {phase} row {row} = {dx}");
        foreach(int level in new[]{1,2,3}){
            int peak=Enumerable.Range(0,100).Max(j=>Math.Abs(TimedBuffRules.ScanlineOffset(level,0,j)));
            Check(peak==(int)(level*3.5),"TIMED scanline amplitude 3.5x level "+level);
            Check(Enumerable.Range(0,480).All(j=>TimedBuffRules.ScanlineOffset(level,17,j)==TimedBuffRules.ScanlineOffset(level,17,j+100)),"TIMED scanline vertical period 100 rows "+level);
            Check(Enumerable.Range(0,480).All(j=>TimedBuffRules.ScanlineOffset(level,0,j)==TimedBuffRules.ScanlineOffset(level,TimedBuffRules.LiquorPhases,j)),"TIMED scanline back home at phase 200 "+level);
            Check(Enumerable.Range(0,100).Any(j=>TimedBuffRules.ScanlineOffset(level,0,j)!=TimedBuffRules.ScanlineOffset(level,1,j)),"TIMED scanline phase moves the wave "+level);
        }
        foreach(int level in new[]{0,4,8,-1})Check(Enumerable.Range(0,200).All(j=>TimedBuffRules.ScanlineOffset(level,j,j)==0),"TIMED scanline only for levels 1 to 3: "+level);
        Check(TimedBuffRules.LiquorPhase(-10)==0&&TimedBuffRules.LiquorPhase(9999)==199&&TimedBuffRules.LiquorPhase(10000)==0,"TIMED phase bounds");

        int[] pixels=Enumerable.Range(0,3*5).ToArray(); // rows [0..4], [5..9], [10..14]
        TimedBuffRules.ShiftScanlines(pixels,5,3,row=>row switch{0=>2,1=>0,_=>-1});
        Check(pixels.SequenceEqual(new[]{0,0,0,1,2, 5,6,7,8,9, 11,12,13,14,14}),"TIMED scanline shift moves each row independently, edges repeat");
        int[] wide=Enumerable.Range(0,4).ToArray();TimedBuffRules.ShiftScanlines(wide,4,1,_=>9);
        Check(wide.All(v=>v==0),"TIMED scanline shift larger than the row stays in bounds");
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
        Check(typeof(ClientForm).GetMethod("CreateHandle",F)!.DeclaringType==typeof(ClientForm)&&typeof(ClientForm).GetMethod("PaintLiquorWave",F)!=null&&typeof(ClientForm).GetMethod("PaintLiquorShake",F)==null,"TIMED client form registers the scanline paint mount, whole-screen shake removed");
        // The mount warps a finished 32-bit frame with the product helper: a vertical white line becomes the wave.
        using var frame=new Bitmap(40,100);
        using(var g=Graphics.FromImage(frame)){g.Clear(Color.Black);g.FillRectangle(Brushes.White,20,0,1,100);}
        var warp=typeof(ClientForm).GetMethod("WarpLiquorFrame",F|System.Reflection.BindingFlags.Static)!;
        warp.Invoke(null,[frame,(Func<int,int>)(row=>TimedBuffRules.ScanlineOffset(3,0,row))]);
        bool all=true;for(int row=0;row<100;row++){int dx=TimedBuffRules.ScanlineOffset(3,0,row);all&=frame.GetPixel(20+dx,row).R==255&&(dx==0||frame.GetPixel(20,row).R==0);}
        Check(all,"TIMED mount moves every scanline by its own table offset");
    }
}
