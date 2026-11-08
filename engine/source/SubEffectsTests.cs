using System.Runtime.CompilerServices;
namespace P06;

// T242／FX2 離線測試：只用合成的影格序列與 list.spr 片段文字，不讀遊戲資源、不開遊戲或網路。
static class SubEffectsTests {
    // list.spr 6325「baby kangaroo」spell（方向 0）同結構的合成序列：第 3 格（0 tick）帶 `]6327`。
    static AnimationStep[] KangarooSpell()=>[
        new("0:0",1),new("0:0",0,null,"<4571"),new("24:0",0,null,"]6327"),new("24:0",5),
        new("24:1",2),new("24:2",5,null,"!"),new("24:3",3),new("24:4",4)];
    // 與 list.spr 6327 同格式的合成條目（fps 24）；子特效本身無 token。
    const string Effect6327="#6327 8 synthetic attack two e\n\t0.animation(1 5,0.4:4 0.0:5 0.1:2 0.2:5 0.3:3)\n\t104.attr(8)\n\t109.effect(5 6328)\n";
    static EffectSprite Chain(int gfx,int next)=>EffectSprite.Parse(gfx,$"#{gfx} 1 synthetic chain\n\t0.animation(0 2,0.1:4 0.0:4]{next})\n");

    [WorldModuleTest("sub-effects")]
    static void Run(WorldModuleTestContext t){
        // 1. token 解析：`]N` 子特效、`{N`／`}N` 淡入淡出；`[`、`<` 是音效不是子特效。
        t.Equal("1054",string.Join(",",FrameTokens.Spawns("]1054!")),"`]1054!` spawns 1054");
        t.Equal(0,FrameTokens.Spawns("[264<313").Length,"sound tokens are not sub-effects");
        t.Equal((int?)300,FrameTokens.FadeIn("<4051{300"),"`{300` fade-in steps");
        t.Equal((int?)100,FrameTokens.FadeOut("}100"),"`}100` fade-out steps");
        t.Equal((int?)null,FrameTokens.FadeIn(""),"no fade token");

        // 2. list.spr 解析保留每格 token；預設 Effect 顯示順序 index1…n-1、最後 index0。
        var e6327=EffectSprite.Parse(6327,Effect6327);
        t.Equal(5,e6327.Kind,"109 kind parsed (ignored by `]`)");
        t.Equal(790,e6327.Duration,"6327 one cycle 208+83+208+125+166 ms");
        var chain=Chain(900,901);
        t.Equal("]901",chain.Steps[1].Events,"EffectStep keeps raw frame tokens");
        var effectLine=ActionTimeline.Of(chain);
        t.Equal(0d,effectLine.Steps[0].Offset,"index1 is displayed first (cursor advanced before first display)");
        t.Equal("]901",effectLine.Steps[0].Events,"`]` on the first displayed frame fires at age 0");

        // 3. 動作時間軸與 ObjectAppearance.At 同尺度：非 breath 依 Duration 縮放 tick。
        var spell=ActionTimeline.Of(KangarooSpell(),false,20*1000d/24);
        t.Check(Math.Abs(spell.Steps[2].Offset-1000d/24)<1e-9,"`]6327` frame starts after 1 tick",spell.Steps[2].Offset);
        t.Check(spell.HasTokens&&!spell.Loop,"spell timeline has tokens and does not loop");
        var slow=ActionTimeline.Of([new("a",1,100,""),new("b",1,50,"]7")],false,150,new ActionCadence(0,0,18,0,150,150,null,0,0,true));
        t.Equal(200d,slow.Steps[1].Offset,"slow cadence doubles each native frame (ActionCadence.Frame)");

        // 4. 追蹤器：每格只觸發一次；新的動作實例會再觸發。
        var tr=new FrameEventTracker();
        t.Equal(0,tr.Advance("o1","spellDir:1000",spell,30,1030).Count(s=>s.Events.Contains(']')),"before `]` frame nothing fires");
        var fired=tr.Advance("o1","spellDir:1000",spell,50,1050);
        t.Equal("]6327",string.Join("|",fired.Where(s=>s.Events.Contains(']')).Select(s=>s.Events)),"crossing the `]` frame fires once");
        t.Equal(0,tr.Advance("o1","spellDir:1000",spell,60,1060).Count(s=>s.Events.Contains(']')),"no duplicate on later frames");
        t.Equal(1,tr.Advance("o1","spellDir:2000",spell,45,2045).Count(s=>s.Events.Contains(']')),"a new action instance fires again");
        var breath=new ActionTimeline([new(0,""),new(100,"]5")],400,true);
        var tb=new FrameEventTracker();
        t.Equal(1,tb.Advance("b","x",breath,150,0).Count(s=>s.Events=="]5"),"first observation: frames within catch-up window");
        t.Equal(1,tb.Advance("b","x",breath,520,0).Count(s=>s.Events=="]5"),"looping breath fires again next cycle");
        var late=new FrameEventTracker().Advance("b","x",breath,100000,0);
        t.Check(late.Count(s=>s.Events=="]5")<=2,"long stall only catches up the last 500 ms",late.Length);

        // 5. 子特效生命週期：位置／方向是生成那格的擁有者，播完一次就刪除；讀不到的 gfx 只記錄不畫。
        var set=new SubEffectSet();var drawn=new List<(long,int,int,int,long)>();
        EffectSprite? Resolve(int g)=>g switch{6327=>e6327,900=>chain,901=>Chain(901,900),_=>null};
        string? Draw(SubEffect e,EffectSprite s,long age){drawn.Add((e.Number,e.X,e.Y,e.Direction,age));return $"{e.Gfx}:{age}";}
        set.Spawn(6327,32600,32900,3,1000,"o1",1,false);
        var v=set.Advance(1000,Resolve,Draw);
        t.Check(v.Length==1&&v[0].X==32600&&v[0].Y==32900&&v[0].Direction==3&&v[0].Gfx==6327,"6327 drawn at the owner's frame position and direction",v);
        t.Equal(1,set.Advance(1789,Resolve,Draw).Length,"still alive 1 ms before its cycle ends");
        t.Equal(0,set.Advance(1790,Resolve,Draw).Length,"removed after one cycle");
        t.Equal(0,set.Live.Count,"no ghost after expiry");
        set.Spawn(999,1,1,0,2000,"o1",1,false);
        t.Equal(0,set.Advance(2000,Resolve,Draw).Length,"unreadable gfx is not drawn");
        t.Equal(1,set.Missing.GetValueOrDefault(999),"unreadable gfx number recorded");
        set.Spawn(6327,1,1,0,3000,"o2",1,true);
        t.Check(set.Advance(3000,Resolve,Draw).Length==0&&set.Live.Count==1,"hidden owner flag copied: alive but not drawn");
        set.Clear();

        // 6. 特效再生特效（遞迴）；互相生成時以深度上限收斂。
        var rec=new SubEffectSet();rec.Spawn(900,5,6,7,0,"magic:1",1,false);
        var rv=rec.Advance(0,Resolve,Draw);
        t.Check(rv.Any(x=>x.Gfx==901&&x.Depth==2&&x.X==5&&x.Y==6&&x.Direction==7),"effect `]` spawns a child at the effect's position/direction",rv);
        t.Check(rec.Live.Count==SubEffectSet.MaxDepth&&rec.Dropped>0,"recursive chain bounded by depth",new{rec.Live.Count,rec.Dropped});
        t.Equal(0,rec.Advance(10000,Resolve,Draw).Length,"whole chain expires");

        // 7. 淡入／淡出：alpha 0→31 與 31→0，每 10 ms 一步（推測單位）。
        var fin=new FrameFade(true,100,0);var fout=new FrameFade(false,100,0);
        t.Check(fin.Alpha(0)==0&&fin.Alpha(500)==16&&fin.Alpha(1000)==31&&fin.Finished(1000),"fade-in 0→31 over N steps");
        t.Check(fout.Alpha(0)==31&&fout.Alpha(1000)==0&&!fout.Finished(5000),"fade-out 31→0 and stays out");
        var bounds=new Rectangle(0,0,3,1);
        var over=new Patch(bounds,[0x7FFF,0x0010,0],[true,true,false],[-1,-1,-1],[]);
        var under=new Patch(bounds,[0x0000,0x0010,0],[true,true,false],[-1,-1,-1],[]);
        var mixed=Renderer.FadeMix(over,under,16);
        t.Check(mixed.Colors[0]==Renderer.Weighted(0x7FFF,0,16)&&mixed.Colors[1]==0x0010&&!mixed.Mask[2],"fade mixes only pixels the fading owner changed",mixed.Colors);

        // 8. Renderer 接點：擁有者跨過 `]` 格時建立子特效、`{` 設定淡入；世界重置清空。
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);
        var session=new WorldSession();
        var owner=new ActionTimeline([new(0,""),new(40,"]6327{100")],200,false);
        r.ObserveOwnerFrames(session,"o7",7,owner,"spellDir:0",30,32610,32910,5,false,10030);
        t.Equal(0,r.SubEffects.Live.Count,"renderer: nothing before the `]` frame");
        r.ObserveOwnerFrames(session,"o7",7,owner,"spellDir:0",60,32611,32911,6,false,10060);
        var child=r.SubEffects.Live.SingleOrDefault();
        t.Check(child is {Gfx:6327,X:32611,Y:32911,Direction:6,At:10040},"renderer: child at current owner position/direction, start = frame time",child);
        t.Check(r.FadeAlphas(10040).TryGetValue(7,out int a0)&&a0==0,"renderer: `{100` starts fade-in at alpha 0 for that owner");
        session.Reset();
        r.ObserveOwnerFrames(session,"o7",7,new ActionTimeline([],0,false),"x",0,0,0,0,false,20000);
        t.Equal(0,r.SubEffects.Live.Count,"world epoch change clears sub-effects");
    }
}
