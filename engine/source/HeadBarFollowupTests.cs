namespace P06.NativeUi;

// T356（#820，R8-A）頭上條離線測試：自己的 x＝P.x − 80（FU8§1.1）、自己的閃爍條（FU8§1.2：35／5、40／無限、
// max(300, 26·HP%)；surf 8393 缺圖不畫，FU8§1.4）。只用合成 surf（純色方塊）與合成狀態；不讀主客戶端或參考客戶端、
// 不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class HeadBarFollowupTests {
    const uint Base=0xFF202020,Hp=0xFFC00000,Mp=0xFF0000C0,Flash=0xFFFFFF00;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Func<int,UiSurface?> Surfs(bool withFlash){
        var d=new Dictionary<int,UiSurface>{[930]=Surface(930,44,5,Base),[931]=Surface(931,44,5,Hp),[932]=Surface(932,44,5,Mp)};
        if(withFlash)d[8393]=Surface(8393,44,5,Flash);
        return id=>d.TryGetValue(id,out var s)?s:null;
    }
    static readonly Rectangle SelfBox=new(380,220,48,64);
    static readonly Point P=new(456,288);
    static HpMpHeadTarget Self(Point? origin=null)=>new(1,true,SelfBox,Origin:origin??P);
    static UiHpMpGameState S(long hp,long now,byte? cls=null,params HpMpHeadTarget[] t)=>new(new UiStaticGameState(true,"me",50,hp,100,50,100,now),t.Length==0?[Self()]:t,cls);
    static uint Px(HpMpHeadBarPanel p,int x,int y){
        using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        return (uint)canvas.Bitmap.GetPixel(x,y).ToArgb();
    }

    [WorldModuleTest("headbar-followup")]
    static void Run(WorldModuleTestContext t){Anchor(t);Checker(t);Warrior(t);Panel(t);State(t);}

    static void Anchor(WorldModuleTestContext t){
        t.Equal(new Point(376,SelfBox.Top-12),HpMpHeadBarPanel.Anchor(Self()),"自己：x＝P.x − 80（P＝(456,288) → 376），y＝外框上 − 12");
        t.Equal(HpMpHeadBarPanel.Anchor(SelfBox),HpMpHeadBarPanel.Anchor(new HpMpHeadTarget(1,true,SelfBox)),"自己沒有 P：退回外框規則");
        var other=new HpMpHeadTarget(2,false,new(500,300,40,60),HpPercent:50,Origin:new Point(10,10));
        t.Equal(new Point(500+20-24,300-12),HpMpHeadBarPanel.Anchor(other),"其他物件不看 P：外框中心 − 24");
        var p=new HpMpHeadBarPanel();p.Update(S(80,0));
        t.Check(p.Plan().Single().At==new Point(376,208),"Plan 用自己的 P",p.Plan().Select(x=>x.At));
    }

    static void Checker(WorldModuleTestContext t){
        t.Equal(910,HpMpHeadBarPanel.BlinkChecker.Interval(35),"間隔 35% → 910 ms");
        t.Equal(300,HpMpHeadBarPanel.BlinkChecker.Interval(10),"間隔 10% → 300 ms（下限）");
        t.Equal(300,HpMpHeadBarPanel.BlinkChecker.Interval(0),"間隔 0% → 300 ms");
        var n=HpMpHeadBarPanel.BlinkChecker.ForClass(null);var c0=HpMpHeadBarPanel.BlinkChecker.ForClass(0);var c6=HpMpHeadBarPanel.BlinkChecker.ForClass(6);
        t.Check(n.Threshold==35&&n.Times==5&&c0.Threshold==35&&c6.Times==5&&!n.Lit&&!n.Initial,"職業 0–6（與未知）：(35, 5, 0)");

        var b=HpMpHeadBarPanel.BlinkChecker.ForClass(1);int iv=HpMpHeadBarPanel.BlinkChecker.Interval(30);   // 780
        b.Step(30,iv);t.Check(!b.Lit,"累計＝間隔還不切換（要超過）");
        b.Step(30,1);t.Check(b.Lit&&b.LitCount==1&&b.Elapsed==0,"超過間隔 → 亮，次數 1",new{b.Lit,b.LitCount,b.Elapsed});
        var seq=new List<bool>();
        for(int i=0;i<12;i++){b.Step(30,iv+1);seq.Add(b.Lit);}
        // 亮 1 之後：滅、亮2、滅、亮3、滅、亮4、滅、亮5、滅（固定）…
        t.Check(seq.SequenceEqual([false,true,false,true,false,true,false,true,false,false,false,false]),"一般職業亮滿 5 次後固定不亮",seq);
        t.Equal(5,b.LitCount,"已亮 5 次");
        b.Step(36,1);t.Check(!b.Lit&&b.LitCount==0&&b.Elapsed==0,"HP% > 35：次數歸 0、回初始");
        b.Step(35,911);t.Check(b.Lit&&b.LitCount==1,"HP 回到門檻以下（含 35）重新開始閃");
        b.Step(80,0);t.Check(!b.Lit,"一般職業 HP 回升立即熄");
    }

    static void Warrior(WorldModuleTestContext t){
        var w=HpMpHeadBarPanel.BlinkChecker.ForClass(7);
        t.Check(w.Threshold==40&&w.Times==-1,"戰士（職業 7）：(40, −1, 0)");
        int lit=0;
        for(int i=0;i<30;i++){w.Step(40,HpMpHeadBarPanel.BlinkChecker.Interval(40)+1);if(w.Lit)lit++;}
        t.Check(lit==15&&w.LitCount==15,"戰士 HP 40% 一直閃（30 段亮 15 次）",new{lit,w.LitCount});
        w.Step(40,1041);t.Check(w.Lit,"準備：亮著");
        w.Step(50,1000);t.Check(w.Lit&&w.LitCount==0,"戰士 HP 回升：目前這段還沒走完先保持亮（次數歸 0）");
        w.Step(50,301);t.Check(!w.Lit&&w.Elapsed==0,"這段走完（> max(300, 26·50)＝1300）才回初始");
        w.Step(50,5000);t.Check(!w.Lit,"之後維持不亮");
    }

    static void Panel(WorldModuleTestContext t){
        var p=new HpMpHeadBarPanel();p.Load(null!,Surfs(withFlash:true));
        p.Update(S(30,1000,0));
        t.Check(p.Blink is {Threshold:35,Lit:false}&&!p.Plan().Single().Blink,"第一幀不亮（初始狀態）");
        p.Update(S(30,1000+781,0));
        var row=p.Plan().Single();
        t.Check(row.Blink&&p.Blink!.LitCount==1,"781 ms 後亮",new{row.Blink,p.Blink?.LitCount});
        int x=row.At.X,y=row.At.Y;
        t.Equal(Flash,Px(p,x+12,y),"閃爍條蓋在 HP 條上（寬 44·30% = 13）");
        t.Equal(Flash,Px(p,x+12,y+3),"高 4 列");
        t.Equal(Base,Px(p,x+13,y),"寬 13 之外是 HP 底");
        t.Equal(Mp,Px(p,x+5,y+4),"MP 條照常（y+4，第 5 列不被閃爍蓋）");
        t.Equal(Base,Px(p,x+22,y+4),"MP 50% = 22 px");

        var none=new HpMpHeadBarPanel();none.Load(null!,Surfs(withFlash:false));
        none.Update(S(30,0,0));none.Update(S(30,781,0));
        t.Check(none.Plan().Single().Blink,"沒有 8393 時檢查器照跑");
        t.Equal(Hp,Px(none,none.Plan().Single().At.X+5,none.Plan().Single().At.Y),"8393 缺圖：不畫閃爍，HP 條照常");

        var war=new HpMpHeadBarPanel();war.Load(null!,Surfs(withFlash:true));
        war.Update(S(38,0,7));war.Update(S(38,1000,7));
        t.Check(war.Blink is {Threshold:40,Times:-1,Lit:true},"戰士 HP 38%：門檻 40 → 閃",war.Blink);
        var nor=new HpMpHeadBarPanel();nor.Update(S(38,0,3));nor.Update(S(38,1000,3));
        t.Check(nor.Blink is {Threshold:35,Lit:false},"一般職業 HP 38% > 35 不閃");

        var hidden=new HpMpHeadBarPanel(()=>HpMpSettings.Default with{ShowCharacterHpMp=false});
        hidden.Update(S(10,0));hidden.Update(S(10,1000));
        t.Check(hidden.Blink==null&&!hidden.Plan().Any(),"showCharacterHPMP 關閉：整組不畫，也沒有檢查器");
        var other=new HpMpHeadBarPanel();
        other.Update(S(10,0,null,new HpMpHeadTarget(2,false,new(500,300,40,60),HpPercent:10)));other.Update(S(10,1000,null,new HpMpHeadTarget(2,false,new(500,300,40,60),HpPercent:10)));
        t.Check(other.Blink==null&&!other.Plan().Single().Blink,"其他物件不閃（kind 0）");
        var gone=new HpMpHeadBarPanel();gone.Update(S(10,0));gone.Update(S(10,1000));
        t.Check(gone.Blink?.Lit==true,"準備：自己亮著");
        gone.Update(new UiHpMpGameState(new UiStaticGameState(true,"me",50,10,null,50,100,1100),[Self()]));
        t.Check(gone.Blink==null,"自己 MaxHP 不明：檢查器清掉");
        var back=new HpMpHeadBarPanel();back.Update(S(10,5000));back.Update(S(10,4000));
        t.Check(back.Blink is {Lit:false,Elapsed:0},"時間倒退當 0 ms");
    }

    static void State(WorldModuleTestContext t){
        var s=new UiHpMpGameState(new UiStaticGameState(true),[],7);
        t.Equal((byte?)7,s.OwnClass,"UiHpMpGameState 帶職業碼");
        t.Equal((byte?)null,((IUiHpMpState)new UiHpMpGameState(new UiStaticGameState(true),[])).OwnClass,"預設職業碼 null");
        t.Equal((Point?)null,new HpMpHeadTarget(1,true,SelfBox).Origin,"HpMpHeadTarget.Origin 預設 null");
    }
}
