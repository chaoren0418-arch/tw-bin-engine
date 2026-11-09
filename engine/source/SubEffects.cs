using System.Text.RegularExpressions;
namespace P06;

// T242／FX2：list.spr 影格 token `]N`（子特效）與 `{N`／`}N`（淡入／淡出）。
// 依據 docs/reference/effects-2.md §2（TW16 sub_A4B060／sub_A4BA20 的 `]` 分支、sub_8ABC10 的特效 `]`）、
// skills-visual.md §2.2（`]` 一律建預設 Effect 類別 sub_8AB390，播完自刪，不看 109 kind）。
// - 位置與方向取「產生它那一格當下」的擁有者，之後不再跟隨（Effect 沒有擁有者指標）。
// - 擁有者的狀態旗標（隱身）會複製到子特效（e+195 = actor+195）。
// - 子特效自己的影格再遇到 `]` 時再生特效（可遞迴，設深度與數量上限防止自我參照無限生成）。
// - 圖檔在執行期由 EffectSpriteCatalog 從原客戶端 Sprite*.pak 讀取；讀不到就不畫，記下 gfx 編號。
public static class FrameTokens {
    static readonly Regex spawn=new(@"\](\d{1,6})",RegexOptions.CultureInvariant),fadeIn=new(@"\{(\d{1,6})",RegexOptions.CultureInvariant),fadeOut=new(@"\}(\d{1,6})",RegexOptions.CultureInvariant);
    public static int[] Spawns(string? events)=>string.IsNullOrEmpty(events)?[]:spawn.Matches(events).Select(m=>int.Parse(m.Groups[1].Value)).Where(g=>g>0).ToArray();
    public static int? FadeIn(string? events)=>Number(fadeIn,events);
    public static int? FadeOut(string? events)=>Number(fadeOut,events);
    public static bool Any(string? events)=>!string.IsNullOrEmpty(events)&&events.AsSpan().IndexOfAny("]{}")>=0;
    static int? Number(Regex r,string? events){if(string.IsNullOrEmpty(events))return null;var m=r.Match(events);return m.Success&&int.Parse(m.Groups[1].Value) is int n&&n>0?n:null;}
}

/// <summary>一個動作（或特效）各影格的開始時間（毫秒，與畫面使用的 elapsed 同一尺度）與 token。</summary>
public sealed record TimelineStep(double Offset,string Events);
public sealed class ActionTimeline {
    public TimelineStep[] Steps {get;}
    public double Length {get;}
    public bool Loop {get;}
    public bool HasTokens {get;}
    public ActionTimeline(TimelineStep[] steps,double length,bool loop){Steps=steps;Length=length;Loop=loop;HasTokens=steps.Any(s=>FrameTokens.Any(s.Events));}
    /// <summary>角色／怪物動作：與 ObjectAppearance.At 與 ActionCadence.Frame 的影格選擇一致。</summary>
    public static ActionTimeline Of(ObjectAppearance a,string action,int direction,ActionCadence? timing=null)=>Of(a.Sequences[(action,direction)],action=="breath",a.Duration(action),timing);
    public static ActionTimeline Of(AnimationStep[] sequence,bool breath,double duration,ActionCadence? timing=null){
        var steps=new List<TimelineStep>();double t=0;
        if(timing!=null&&(timing.Slow||timing.Haste)){
            // ActionCadence.Frame：慢速每格 ×2（整數毫秒），加速每格 HasteFrameMs；兩者都不循環。
            foreach(var s in sequence){steps.Add(new(t,s.Events));int ms=s.Milliseconds??s.Ticks*1000/24;t+=timing.Slow?2d*ms:ActionCadence.HasteFrameMs(ms);}
            return new(steps.ToArray(),t,false);
        }
        if(sequence.All(s=>s.Milliseconds!=null)){foreach(var s in sequence){steps.Add(new(t,s.Events));t+=s.Milliseconds!.Value;}return new(steps.ToArray(),t,breath);}
        double total=sequence.Sum(s=>(double)s.Ticks),scale=breath?1000d/24:total>0?duration/total:0;
        foreach(var s in sequence){steps.Add(new(t*scale,s.Events));t+=s.Ticks;}
        return new(steps.ToArray(),total*scale,breath);
    }
    /// <summary>預設 Effect（sub_8AB390／8ABC10）：建立後先把游標推進一格，顯示順序為 index1…n-1，最後 index0（同 EffectSprite.At）。</summary>
    public static ActionTimeline Of(EffectSprite effect,bool loop=false){
        var steps=new List<TimelineStep>();double t=0;int n=effect.Steps.Length;
        for(int i=1;i<=n;i++){var s=effect.Steps[i%n];steps.Add(new(t,s.Events));t+=s.Milliseconds;}
        return new(steps.ToArray(),t,loop);
    }
    /// <summary>封包特效（MagicMotion.PureFrame）：同上的顯示順序，毫秒序列。</summary>
    public static ActionTimeline Pure(AnimationStep[] sequence,bool loop){
        var steps=new List<TimelineStep>();double t=0;int n=sequence.Length;
        for(int i=1;i<=n;i++){var s=sequence[i%n];steps.Add(new(t,s.Events));t+=s.Milliseconds??s.Ticks*1000d/24;}
        return new(steps.ToArray(),t,loop);
    }
    /// <summary>開始時間落在 (after, upTo] 的影格；循環動作會展開每一輪。</summary>
    public IEnumerable<TimelineStep> Crossed(double after,double upTo){
        if(upTo<=after||Steps.Length==0)yield break;
        if(!Loop){foreach(var s in Steps)if(s.Offset>after&&s.Offset<=upTo)yield return s;yield break;}
        if(Length<=0)yield break;
        for(long k=(long)Math.Floor(Math.Max(after,0)/Length);k*Length<=upTo;k++)
            foreach(var s in Steps){double o=k*Length+s.Offset;if(o>after&&o<=upTo)yield return s with{Offset=o};}
    }
}

/// <summary>記住每個擁有者上一次畫到的動作時間，找出這一次新跨過的影格（每格只觸發一次）。</summary>
public sealed class FrameEventTracker {
    // 畫面停頓（視窗最小化等）後只補最近這段，不一次補出整串舊特效；新出現的擁有者也只看這段。
    public const double CatchUpMs=500;
    readonly Dictionary<string,(string Instance,double Elapsed,long Seen)> last=new(StringComparer.Ordinal);
    public int Count=>last.Count;
    public TimelineStep[] Advance(string owner,string instance,ActionTimeline timeline,double elapsed,long now){
        double after=-1;
        if(last.TryGetValue(owner,out var p)&&p.Instance==instance&&elapsed>=p.Elapsed)after=p.Elapsed;
        after=Math.Max(after,elapsed-CatchUpMs);
        last[owner]=(instance,elapsed,now);
        if(last.Count>1024)foreach(var k in last.Where(x=>now-x.Value.Seen>10000).Select(x=>x.Key).ToArray())last.Remove(k);
        return timeline.Crossed(after,elapsed).ToArray();
    }
    public void Remove(string owner)=>last.Remove(owner);
    public void Clear()=>last.Clear();
}

/// <summary>`{N` 淡入／`}N` 淡出（sub_A4F160／sub_A4F2B0）：alpha 0–31，每 10 個時間單位走一步。</summary>
public sealed record FrameFade(bool In,int Steps,long At){
    // sub_A231C0(10,…) 的 10 視為毫秒（effects-2.md §2.1(c) 標為推測）。
    public const int TickMs=10;
    public int Alpha(long now){
        long k=Math.Max(0,now-At)/TickMs;
        return In?(int)Math.Min(31,k*32/Steps):(int)Math.Max(0,31-k*31/Steps);
    }
    public bool Finished(long now)=>In&&Alpha(now)>=31;
}

public sealed record SubEffect(long Number,int Gfx,int X,int Y,int Direction,long At,string Source,int Depth,bool Hidden);
/// <summary>R2-H：飛行物（109 kind 3／4／5 與箭矢）影格上的 `]`。原版飛行類別的每步函式是否處理 `]` 待查證（Q-4），只記錄、不生成。</summary>
public sealed record WithheldSubEffect(string Owner,int OwnerGfx,int Gfx,int X,int Y,int Direction,long At,string Reason);
public sealed record SubEffectVisual(long Number,int Gfx,int X,int Y,int Direction,string Frame,string Source,int Depth,long At);

/// <summary>`]` 生出的一次性預設 Effect 清單：每格處理自己的 `]`（可遞迴），播完一次就移除。</summary>
public sealed class SubEffectSet {
    public const int MaxLive=256,MaxDepth=8;
    readonly List<SubEffect> live=[];
    readonly FrameEventTracker frames=new();
    readonly SortedDictionary<int,int> missing=[];
    long number;
    public IReadOnlyList<SubEffect> Live=>live;
    /// <summary>讀不到圖（原客戶端沒有這個 gfx 或 SPR）的 gfx 編號與次數。</summary>
    public IReadOnlyDictionary<int,int> Missing=>missing;
    public int Dropped {get;private set;}
    public void Clear(){live.Clear();frames.Clear();}
    public SubEffect? Spawn(int gfx,int x,int y,int direction,long at,string source,int depth,bool hidden){
        if(depth>MaxDepth||live.Count>=MaxLive){Dropped++;return null;}
        var e=new SubEffect(++number,gfx,x,y,direction,at,source,depth,hidden);live.Add(e);return e;
    }
    /// <param name="draw">畫出該格並回傳影格 key；回傳 null 表示這格沒畫（例如不在畫面範圍）。</param>
    public SubEffectVisual[] Advance(long now,Func<int,EffectSprite?> resolve,Func<SubEffect,EffectSprite,long,string?> draw){
        var visuals=new List<SubEffectVisual>();
        // 這一輪新生的子特效附加在尾端，同一輪就會處理（含它自己的 `]`）。
        for(int i=0;i<live.Count;i++){
            var e=live[i];var sprite=resolve(e.Gfx);string key="e"+e.Number;
            if(sprite==null){
                missing[e.Gfx]=missing.GetValueOrDefault(e.Gfx)+1;
                System.Diagnostics.Trace.WriteLine($"Sub-effect gfx {e.Gfx} unavailable (source {e.Source}); not drawn");
                frames.Remove(key);live.RemoveAt(i--);continue;
            }
            long age=now-e.At;if(age<0)continue;
            var timeline=ActionTimeline.Of(sprite);
            if(timeline.HasTokens)foreach(var step in frames.Advance(key,"",timeline,Math.Min(age,sprite.Duration),now))
                foreach(int gfx in FrameTokens.Spawns(step.Events))Spawn(gfx,e.X,e.Y,e.Direction,e.At+(long)step.Offset,$"effect:{e.Gfx}",e.Depth+1,e.Hidden);
            if(age>=sprite.Duration){frames.Remove(key);live.RemoveAt(i--);continue;}
            if(!e.Hidden&&draw(e,sprite,age) is string frame)visuals.Add(new(e.Number,e.Gfx,e.X,e.Y,e.Direction,frame,e.Source,e.Depth,e.At));
        }
        return visuals.ToArray();
    }
}

public sealed partial class Renderer {
    SubEffectSet? subEffects;FrameEventTracker? ownerFrames;Dictionary<uint,FrameFade>? fades;int subEffectEpoch=-1;
    Dictionary<(ObjectAppearance,string,int,ActionCadence?),ActionTimeline>? timelines;
    public SubEffectSet SubEffects=>subEffects??=new();
    public SubEffectVisual[] SubEffectVisuals {get;private set;}=[];
    FrameEventTracker? flightFrames;List<WithheldSubEffect>? withheld;
    public const int MaxWithheld=64;
    /// <summary>最近被擋下（未生成）的飛行物 `]`，最多 64 筆；世界重置清空。</summary>
    public IReadOnlyList<WithheldSubEffect> WithheldSubEffects=>withheld??=[];
    void SubEffectEpoch(WorldSession session){
        if(subEffectEpoch==session.WorldEpoch)return;
        subEffects?.Clear();ownerFrames?.Clear();fades?.Clear();flightFrames?.Clear();withheld?.Clear();subEffectEpoch=session.WorldEpoch;
    }
    /// <summary>飛行中的特效（循環播放）新跨過的 `]`：只記錄為資料（Q-4 未查證），不建立子特效。</summary>
    internal void WithholdFlightFrames(WorldSession session,string owner,EffectSprite? effect,long age,int x,int y,int direction,long now){
        SubEffectEpoch(session);if(effect==null||age<0)return;
        var timeline=ActionTimeline.Of(effect,true);if(!timeline.HasTokens)return;
        foreach(var step in (flightFrames??=new()).Advance(owner,"",timeline,age,now))
            foreach(int gfx in FrameTokens.Spawns(step.Events)){
                var list=withheld??=[];if(list.Count>=MaxWithheld)list.RemoveAt(0);
                list.Add(new(owner,effect.Gfx,gfx,x,y,direction,now-(long)Math.Max(0,age-step.Offset),"`]` on flying classes unverified (Q-4)"));
            }
    }
    ActionTimeline Timeline(ObjectAppearance a,string action,int direction,ActionCadence? timing){
        timelines??=[];var key=(a,action,direction,timing);
        if(!timelines.TryGetValue(key,out var t)){if(timelines.Count>=512)timelines.Clear();t=ActionTimeline.Of(a,action,direction,timing);timelines[key]=t;}
        return t;
    }
    /// <summary>角色／怪物（含自己）每次組畫面時呼叫：處理新跨過的 `]`／`{`／`}`。fadeKey 是 Draw.ObjectId（自己為 0）。</summary>
    internal void ObserveOwnerFrames(WorldSession session,string owner,uint fadeKey,ObjectAppearance a,string action,int direction,ActionCadence? timing,string instance,double elapsed,int x,int y,bool hidden,long now){
        if(!a.Supports(action,direction))return;
        ObserveOwnerFrames(session,owner,fadeKey,Timeline(a,action,direction,timing),instance,elapsed,x,y,direction,hidden,now);
    }
    internal void ObserveOwnerFrames(WorldSession session,string owner,uint fadeKey,ActionTimeline timeline,string instance,double elapsed,int x,int y,int direction,bool hidden,long now){
        SubEffectEpoch(session);if(!timeline.HasTokens||double.IsInfinity(elapsed)||elapsed>=double.MaxValue/2)return;
        foreach(var step in (ownerFrames??=new()).Advance(owner,instance,timeline,elapsed,now)){
            long at=now-(long)Math.Max(0,elapsed-step.Offset);
            foreach(int gfx in FrameTokens.Spawns(step.Events))SubEffects.Spawn(gfx,x,y,direction,at,owner,1,hidden);
            if(FrameTokens.FadeIn(step.Events) is int n)(fades??=[])[fadeKey]=new(true,n,at);
            if(FrameTokens.FadeOut(step.Events) is int m)(fades??=[])[fadeKey]=new(false,m,at);
        }
    }
    /// <summary>預設 Effect 類別的特效物件（S30/S42 預設類、S55/S106 封包特效）每格的 `]`。</summary>
    internal void ObserveEffectFrames(WorldSession session,string owner,ActionTimeline timeline,double age,int x,int y,int direction,long now)=>
        ObserveOwnerFrames(session,owner,uint.MaxValue,timeline,"",timeline.Loop?age:Math.Min(age,timeline.Length),x,y,direction,false,now);
    /// <summary>目前仍在淡入／淡出中的擁有者與 alpha（31 已完成的淡入會移除）。</summary>
    public IReadOnlyDictionary<uint,int> FadeAlphas(long now){
        if(fades==null||fades.Count==0)return new Dictionary<uint,int>();
        foreach(var k in fades.Where(f=>f.Value.Finished(now)).Select(f=>f.Key).ToArray())fades.Remove(k);
        return fades.Where(f=>f.Key!=uint.MaxValue).ToDictionary(f=>f.Key,f=>f.Value.Alpha(now));
    }
    void ComposeSubEffects(WorldSession session,List<Draw> draws,long now){
        SubEffectEpoch(session);
        if(subEffects==null||subEffects.Live.Count==0){SubEffectVisuals=[];return;}
        SubEffectVisuals=subEffects.Advance(now,NativeEffect,(e,_,age)=>DrawNativeEffect(e.Gfx,e.Direction,e.X,e.Y,age,session,draws,"sub-effect")?.Key);
    }
    // 淡入淡出（actor+560 alpha）：同一組 commands 少畫該擁有者再組一次，兩張逐像素以 Weighted 混合。
    // Weighted 是線性的，被遮住的像素兩張相同、混合後不變，所以與「該擁有者以 alpha 畫入」等價（screen／multiply 疊在其上時為近似）。
    Patch ComposeFadedPatch(WorldSession session,Rectangle area,List<Draw> commands,SceneOpacity? opacity,long now){
        var patch=ComposeAfflictionPatch(session,area,commands,opacity);
        foreach(var (id,alpha) in FadeAlphas(now).OrderBy(f=>f.Key)){
            if(alpha>=31)continue;
            bool Faded(Draw d)=>d.Layer is "actor" or "shadow"&&d.ObjectId==id;
            if(!commands.Any(Faded))continue;
            var under=ComposeAfflictionPatch(session,area,commands.Where(d=>!Faded(d)).ToList(),opacity);
            patch=alpha<=0?under:FadeMix(patch,under,alpha);
        }
        return patch;
    }
    public static Patch FadeMix(Patch over,Patch under,int alpha){
        if(over.Bounds!=under.Bounds)return over;
        var colors=new ushort[over.Colors.Length];var mask=new bool[colors.Length];
        for(int i=0;i<colors.Length;i++){
            mask[i]=over.Mask[i]||under.Mask[i];
            colors[i]=!over.Mask[i]?under.Colors[i]:Weighted(over.Colors[i],under.Mask[i]?under.Colors[i]:(ushort)0,alpha);
        }
        return over with{Colors=colors,Mask=mask};
    }
}
