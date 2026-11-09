using P06.NativeUi;
namespace P06 {

// T246／#607（R2-O，PBW-A）：S250 子碼 83（全畫面效果）與 84（畫面中央綠字訊息）。
// 依據：docs/reference/packetbox-windows.md（PBW）§2.5、§2.6、§6；docs/reference/ui-layout.md「(B) 中央訊息佇列」（V2）。
// 原版 TW16 S_EVENT（op 108＝3.81 S250）處理函式 sub_9357E0（VA 0x9357E0／FO 0x534BE0）。本任務另以 Capstone 唯讀反組譯
// TW1608262200.bin（SHA-256 e12949cf…09641a2）補讀下列細節（.text FO＝VA − 0x400C00），腳本不進 repo，數值寫在報告：
//
// 83（0x53）讀 D（3.81 S_PacketBoxGree 寫 D；S_GlobalEffect 只寫 1 byte C，封包補 0 後讀 D 值相同，所以「剩餘 ≥4 讀 D、否則 C」）：
//   1  已驗證：word_154461C＝15，sub_A231C0(100, sub_EFDFD0)；sub_EFDFD0（FO 0xAFD3D0）每 30 ms 減 1（push 1Eh），到 0 停。
//      繪製端 sub_F0F0C0 的 0xF0F737–0xF0F76F：計數器非 0 時以 surf 0x5C1（1473）在 UI 原點、第 4 參數＝計數器呼叫 sub_7D7740
//      （透過函式指標 [0x14D7DCC] 畫）。也就是說 1 是「surf 1473 疊在畫面上、強度 15→1」，不是平移震動（PBW 的「短震動」是推定）。
//      參數 15→1 的意義（alpha？畫格？）沒有追，本模組只記計數器（資料），不畫 surf 1473（待驗證）。
//   2  已驗證：case 內 byte_13CE5DE 為 0 → 設 1 並 sub_A231C0(50, sub_F04260)；為 1 → 設 0（0x93B93C–0x93B961）。
//      sub_F04260（FO 0xB03660）：旗標開時依 dword_1544644 % 12 查 12 步表寫 dword_154463C／dword_1544640，計數器
//      < dword_138BE6C×3 時 +1、否則旗標清 0、計數器歸 0；然後以 17 ms（push 11h）再排自己。旗標關時偏移與計數器歸 0、不再排。
//      dword_138BE6C 的 .data 初值是 8（FO 0xF8A66C），所以 24 步後自己停（推定：執行期沒有被改；寫入者 0x8ABD1C 等未追）。
//      繪製端 0xF0FD80–0xF0FEB9：世界畫面的目的矩形各邊減去 (dx,dy)，露出的邊用調色盤[0]（黑）補 → 世界平移 (−dx,−dy)。
//   3  已驗證（0x93B96D–0x93BAE4）：gfx 表 {0x7FA,0x7EE,0x7ED}＝{2042,2030,2029}；走物件清單 0x153CEB0，物件 +0xC4（196）為真者
//      取 +0x10／+0x14 座標，以 rand()%3 選 gfx 建立特效物件，每個之後 Sleep(10)，最多 5 個。+0xC4 旗標的意義未查（待驗證）：
//      本模組以「自己＋可見物件（依物件 id）」當候選，特效放在該物件目前座標（不跟隨）。
//   其他值：sub_A7C3F0 從通知清單 dword_153F574 移除 id＝值的項目，沒有畫面效果；本模組只計數。
// 84（0x54）讀 C(type) S(text)：
//   1／2 已驗證（PBW§2.6、V2(B)）：sub_F04180→sub_F18F40 加入中央佇列：7000 ms、字色調色盤[2]（綠）、外框[0]（黑），第 5 參數＝(type≠1)；
//        已有 ≥14 筆時先刪最舊的，新的一筆放開頭。繪製 sub_F188A0／sub_F19680：游標 y 從 136 起、每筆先上移 15 再畫，最新一筆 y＝121，
//        x＝400 − 字寬/2，y≤0 不畫（800×600 最多 9 行）；加入時刻＋毫秒 < 現在者不再顯示。
//        `$` 代換（sub_A11520）規則待驗證：本模組在畫面層用引擎既有的 `$nnn` 名稱表（ClientForm.ResolveNpcText，與紅字面板相同）。
//        第 5 參數（+268）的作用沒查，只記資料；3.81 只送類型 2。
//   3／4 宿主是 RoundScoreUI（FU3§5.4），用途待驗證：本模組不處理（回 false，記為未處理子碼）。
// 不做：surf 1473 疊圖的繪製、晃動時露出邊補黑（引擎只平移，露出處是畫面底色）、83/2 在 17 ms 內快速開關造成的雙計時器。

/// <summary>中央佇列的一筆（V2 sub_F191A0 的欄位）。Flag＝第 5 參數 (type≠1)，作用待驗證。</summary>
public sealed record CenterMessage(long Sequence,byte Type,string Raw,long At,int DurationMs,int ColorIndex,int OutlineIndex,bool Flag){
    /// <summary>原版過期條件：加入時刻＋毫秒 &lt; 現在。</summary>
    public bool Shown(long now)=>At+DurationMs>=now;
}

public static class PacketBoxScreenRules {
    public const byte Opcode=250,GlobalEffect=83,GreenMessage=84;
    public const uint Flash=1,Sway=2,Fireworks=3;
    // 84
    public const int MessageMs=7000,MaxMessages=14,TextColorIndex=2,OutlineColorIndex=0;
    public const int CursorStartY=136,LineStep=15,CenterX=400,LineHeight=12;
    // 83/1：sub_A231C0(100)，之後每 30 ms −1。
    public const int FlashStart=15,FlashDelayMs=100,FlashStepMs=30,FlashSurf=0x5C1;
    // 83/2：sub_A231C0(50)，之後每 17 ms；24 步（dword_138BE6C 初值 8 ×3）後自己停。
    public const int SwayDelayMs=50,SwayStepMs=17,SwayLimit=8*3;
    /// <summary>sub_F04260 的 12 步表（dword_154463C, dword_1544640），索引＝計數器 % 12。</summary>
    public static readonly Point[] SwayTable=[new(-1,-1),new(-2,-2),new(-1,-1),new(1,1),new(2,2),new(1,2),new(-1,2),new(-2,2),new(-1,1),new(1,-1),new(2,-2),new(1,-1)];
    // 83/3
    public static readonly int[] FireworkGfx=[2042,2030,2029];
    public const int FireworkMax=5,FireworkSpacingMs=10;

    /// <summary>83 的值：剩餘 ≥4 byte 讀 D（TW16 寬度），否則讀 C（3.81 S_GlobalEffect）；其後只允許補 0。</summary>
    public static uint ReadEffect(byte[] plain){
        if(plain.Length<3||plain[0]!=Opcode||plain[1]!=GlobalEffect)throw new ProtocolException("not S250/83");
        int rest=plain.Length-2;
        uint v=rest>=4?BitConverter.ToUInt32(plain,2):plain[2];
        int used=rest>=4?4:1;
        for(int i=2+used;i<plain.Length;i++)if(plain[i]!=0)throw new ProtocolException("nonzero S250/83 tail");
        return v;
    }

    /// <summary>83/1 計數器（word_154461C）：收到後 elapsed ms 時的值；0＝不畫。</summary>
    public static int FlashLevel(long elapsed){
        if(elapsed<0)return 0;
        if(elapsed<FlashDelayMs)return FlashStart;
        return (int)Math.Max(0,FlashStart-1-(elapsed-FlashDelayMs)/FlashStepMs);
    }
    /// <summary>83/1 結束時刻（相對收到）：計數器在第 15 次減到 0。</summary>
    public static long FlashEndMs=>FlashDelayMs+(long)(FlashStart-1)*FlashStepMs;

    /// <summary>
    /// 83/2 原版寫入的偏移 (dword_154463C, dword_1544640)。elapsed＝開啟後經過的 ms；offElapsed＝收到關閉時（null＝沒關）。
    /// 第 k 次 tick 在 50＋17k ms：旗標開著就寫表[k % 12]，k＝24 那次同時清旗標；下一次 tick（或關閉後的第一次 tick）歸 0。
    /// </summary>
    public static Point SwayRaw(long elapsed,long? offElapsed){
        if(elapsed<SwayDelayMs)return Point.Empty;
        long k=(elapsed-SwayDelayMs)/SwayStepMs;
        if(k>SwayLimit)return Point.Empty;
        long tick=SwayDelayMs+k*SwayStepMs;
        if(offElapsed is long off&&off<=tick)return Point.Empty;
        return SwayTable[(int)(k%SwayTable.Length)];
    }
    /// <summary>晃動已結束（之後一定是 0）。</summary>
    public static bool SwayEnded(long elapsed,long? offElapsed){
        if(elapsed<SwayDelayMs)return false;
        long k=(elapsed-SwayDelayMs)/SwayStepMs;
        if(k>SwayLimit)return true;
        return offElapsed is long off&&off<=SwayDelayMs+k*SwayStepMs;
    }
    /// <summary>原版把世界的目的矩形減去偏移，所以畫面平移量是負的偏移。</summary>
    public static Point ScreenOffset(Point raw)=>new(-raw.X,-raw.Y);
    /// <summary>旗標 byte_13CE5DE 目前是否為 1（開啟且尚未自己停）：第 24 次 tick（50＋17×24 ms）清旗標。</summary>
    public static bool SwayFlag(long elapsed,long? offElapsed)=>offElapsed==null&&elapsed<SwayDelayMs+(long)SwayLimit*SwayStepMs;

    /// <summary>第 i 行（0＝最新）的 y：136 − 15(i+1)；≤0 不畫。</summary>
    public static int LineY(int index)=>CursorStartY-LineStep*(index+1);
    public static int MaxVisibleLines{get{int n=0;while(LineY(n)>0)n++;return n;}}
    public static int LineX(int width)=>CenterX-width/2;
    /// <summary>`$nnn` 名稱代換（畫面層）；沒有名稱表時原樣。</summary>
    public static string Resolve(string raw,Func<string,string>? names)=>names==null?raw:names(raw);
}

public sealed partial class WorldSession {
    readonly object packetBoxScreenGate=new();
    readonly LinkedList<CenterMessage> centerMessages=new();
    long centerMessageSequence,screenFlashAt=-1,swayOnAt=-1,swayOffAt=-1;
    int screenFlashCount,swayToggles,fireworkBursts,effectOtherCount;uint lastEffectOther;
    readonly Dictionary<string,int> packetBoxScreenCounts=new(StringComparer.Ordinal);
    /// <summary>測試用：時鐘與 rand()（回 0..n−1）。</summary>
    internal Func<long> PacketBoxScreenClock=()=>Environment.TickCount64;
    internal Func<int,int> PacketBoxScreenRandom=n=>Random.Shared.Next(n);

    /// <summary>中央佇列（最新在前），含已過期但尚未被擠掉的筆。</summary>
    public CenterMessage[] CenterMessages{get{lock(packetBoxScreenGate)return [..centerMessages];}}
    /// <summary>目前要顯示的中央訊息（最新在前）。</summary>
    public CenterMessage[] VisibleCenterMessages(long now){lock(packetBoxScreenGate)return centerMessages.Where(m=>m.Shown(now)).ToArray();}
    /// <summary>83/1 計數器目前值（0＝沒有）；surf 1473 的畫法待驗證，只當資料。</summary>
    public int ScreenFlashLevel{get{long now=PacketBoxScreenClock();lock(packetBoxScreenGate)return screenFlashAt<0?0:PacketBoxScreenRules.FlashLevel(now-screenFlashAt);}}
    /// <summary>83/2 目前的畫面平移（view px）；給 Renderer.ScreenShake 掛點。</summary>
    public Point ScreenShakeOffset{get{
        long now=PacketBoxScreenClock();
        lock(packetBoxScreenGate){
            if(swayOnAt<0)return Point.Empty;
            long? off=swayOffAt<0?null:swayOffAt-swayOnAt;
            if(PacketBoxScreenRules.SwayEnded(now-swayOnAt,off)){swayOnAt=-1;swayOffAt=-1;return Point.Empty;}
            return PacketBoxScreenRules.ScreenOffset(PacketBoxScreenRules.SwayRaw(now-swayOnAt,off));
        }
    }}
    /// <summary>83/3 收到的次數。</summary>
    public int FireworkBursts{get{lock(packetBoxScreenGate)return fireworkBursts;}}
    public event Action<int>? PacketBoxScreenChanged;

    [WorldReceive(PacketBoxScreenRules.Opcode,Subtype=PacketBoxScreenRules.GlobalEffect)]
    bool ReceivePacketBoxGlobalEffect(byte[] plain){
        uint v=PacketBoxScreenRules.ReadEffect(plain);
        long now=PacketBoxScreenClock();string key;
        switch(v){
        case PacketBoxScreenRules.Flash:
            lock(packetBoxScreenGate){screenFlashAt=now;screenFlashCount++;}
            key="flash";break;
        case PacketBoxScreenRules.Sway:
            lock(packetBoxScreenGate){
                long? off=swayOffAt<0?null:swayOffAt-swayOnAt;
                bool on=swayOnAt>=0&&PacketBoxScreenRules.SwayFlag(now-swayOnAt,off);
                if(on)swayOffAt=now;else{swayOnAt=now;swayOffAt=-1;}
                swayToggles++;
            }
            key="sway";break;
        case PacketBoxScreenRules.Fireworks:
            Fireworks(now);key="fireworks";break;
        default:
            lock(packetBoxScreenGate){effectOtherCount++;lastEffectOther=v;}
            key="other";break;
        }
        lock(packetBoxScreenGate)packetBoxScreenCounts["83/"+key]=packetBoxScreenCounts.GetValueOrDefault("83/"+key)+1;
        PacketBoxScreenChanged?.Invoke(PacketBoxScreenRules.GlobalEffect);
        return true;
    }
    void Fireworks(long now){
        // 候選：自己＋可見物件（+0xC4 旗標的意義待驗證）。座標取收到時的位置。
        var spots=new List<(ushort X,ushort Y)>();
        if(Actor is {} a)spots.Add((a.X,a.Y));
        foreach(var o in Objects){if(spots.Count>=PacketBoxScreenRules.FireworkMax)break;if(!o.Dead&&o.ObjectId!=Actor?.ObjectId)spots.Add((o.X,o.Y));}
        int i=0;
        foreach(var (x,y) in spots.Take(PacketBoxScreenRules.FireworkMax)){
            int n=PacketBoxScreenRules.FireworkGfx.Length,gfx=PacketBoxScreenRules.FireworkGfx[Math.Abs(PacketBoxScreenRandom(n)%n)];   // rand()%3
            AddMagicEffect(new(++MagicEffectCount,0,0,gfx,0,0,0,x,y,x,y,now+(long)i*PacketBoxScreenRules.FireworkSpacingMs));
            i++;
        }
        lock(packetBoxScreenGate)fireworkBursts++;
    }

    [WorldReceive(PacketBoxScreenRules.Opcode,Subtype=PacketBoxScreenRules.GreenMessage)]
    bool ReceivePacketBoxGreenMessage(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=PacketBoxScreenRules.Opcode||r.C()!=PacketBoxScreenRules.GreenMessage)throw new ProtocolException("not S250/84");
        byte type=r.C();
        if(type is not (1 or 2))return false;   // 3／4：RoundScoreUI，用途待驗證；其他值原版未見
        string text=r.S();r.Finish();
        long now=PacketBoxScreenClock();
        lock(packetBoxScreenGate){
            while(centerMessages.Count>=PacketBoxScreenRules.MaxMessages)centerMessages.RemoveLast();
            centerMessages.AddFirst(new CenterMessage(++centerMessageSequence,type,text,now,PacketBoxScreenRules.MessageMs,PacketBoxScreenRules.TextColorIndex,PacketBoxScreenRules.OutlineColorIndex,type!=1));
            packetBoxScreenCounts["84/"+type]=packetBoxScreenCounts.GetValueOrDefault("84/"+type)+1;
        }
        PacketBoxScreenChanged?.Invoke(PacketBoxScreenRules.GreenMessage);
        return true;
    }

    [WorldReset]
    void ResetPacketBoxScreen(){
        lock(packetBoxScreenGate){
            centerMessages.Clear();centerMessageSequence=0;screenFlashAt=-1;swayOnAt=-1;swayOffAt=-1;
            screenFlashCount=0;swayToggles=0;fireworkBursts=0;effectOtherCount=0;lastEffectOther=0;packetBoxScreenCounts.Clear();
        }
    }

    [WorldSnapshot("packetBoxScreen")]
    object? PacketBoxScreenSnapshot(){
        long now=PacketBoxScreenClock();var shake=ScreenShakeOffset;int flash=ScreenFlashLevel;
        lock(packetBoxScreenGate)return new{
            packets=packetBoxScreenCounts.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value),
            centerMessages=centerMessages.Select(m=>new{sequence=m.Sequence,type=m.Type,raw=m.Raw,at=m.At,durationMs=m.DurationMs,colorIndex=m.ColorIndex,outlineIndex=m.OutlineIndex,flag=m.Flag,shown=m.Shown(now)}).ToArray(),
            flash=new{level=flash,count=screenFlashCount,surf=PacketBoxScreenRules.FlashSurf,drawn=false},
            sway=new{active=swayOnAt>=0,toggles=swayToggles,offset=new{x=shake.X,y=shake.Y}},
            fireworks=fireworkBursts,
            other=new{count=effectOtherCount,last=lastEffectOther},
        };
    }
}

/// <summary>中央訊息面板的資料來源（轉接 WorldSession）。</summary>
public sealed class WorldCenterMessageSource(Func<WorldSession> world,Func<string,string>? names):ICenterMessageSource {
    public CenterMessage[] Visible(long now)=>world().VisibleCenterMessages(now);
    public Func<string,string>? Names=>names;
}

public sealed partial class ClientForm {
    bool packetBoxScreenHooked;
    // 掛點：其他模組已用掉 OnLoad／OnShown／OnClientSizeChanged 等；OnInvalidated 沒有人用，每次 RefreshScene 的 Invalidate() 都會進來。
    // 第一次進來時把 Renderer.ScreenShake 掛點接到本連線的 83/2 晃動；原生 UI 開著時把中央訊息面板接上 WorldSession。
    protected override void OnInvalidated(InvalidateEventArgs e){
        AttachPacketBoxScreen();
        base.OnInvalidated(e);
    }
    void AttachPacketBoxScreen(){
        if(renderer is null)return;
        if(!packetBoxScreenHooked){packetBoxScreenHooked=true;renderer.ScreenShake??=()=>login.World.ScreenShakeOffset;}
        if(NativeUiActive&&NativeUiRoot?.Find(CenterMessagePanel.PanelId) is CenterMessagePanel panel&&panel.Source==null)
            panel.Source=new WorldCenterMessageSource(()=>login.World,ResolveNpcText);
    }
}
}

namespace P06.NativeUi {

/// <summary>中央訊息資料來源：目前要顯示的訊息（最新在前）與 `$nnn` 名稱表。</summary>
public interface ICenterMessageSource {
    CenterMessage[] Visible(long now);
    Func<string,string>? Names {get;}
}

/// <summary>畫好的一行：第幾行（0＝最新）、代換後的原文（色碼保留）、y。</summary>
public sealed record CenterMessageLine(int Index,string Raw,int Y,ushort Color,ushort Outline);

/// <summary>
/// V2 (B) 中央訊息佇列（sub_F188A0／sub_F19680）：World 層、不接收輸入（同 RED_NOTICE 的理由），沒有 XML 版面。
/// 最新一筆 y＝121，往上每行 15 px，y≤0 不畫；x＝400 − 字寬/2（字寬去掉 \f 色碼後量）。字與 1 px 外框同紅字面板的畫法（推定：
/// sub_F19680 的畫字函式沒追，比照 sub_841770）。
/// </summary>
public sealed class CenterMessagePanel:IUiPanel {
    [UiPanel(155)] internal static UiPanelRegistration Registration=>new(CenterMessagePanel.PanelId,"uistatus86",_=>new CenterMessagePanel());   // R2-O #607 (no XML: V2 hard-coded layout)
    public const string PanelId="CENTER_MESSAGE";
    /// <summary>y 1..121 的 9 行，加外框 ±1。</summary>
    public static readonly Rectangle Band=new(0,0,UiRoot.LogicalWidth,PacketBoxScreenRules.CursorStartY);

    public ICenterMessageSource? Source{get;set;}
    readonly List<CenterMessageLine> lines=[];
    public IReadOnlyList<CenterMessageLine> Lines=>lines;
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.World;
    public bool Visible=>lines.Count>0;
    public Rectangle Bounds=>Visible?Band:Rectangle.Empty;

    public void Load(UiLayout layout,SurfCatalog surfs){}

    public void Update(IUiGameState state){
        lines.Clear();
        if(!state.InWorld||Source is not {} src)return;
        var shown=src.Visible(state.Now);
        for(int i=0;i<shown.Length;i++){
            int y=PacketBoxScreenRules.LineY(i);
            if(y<=0)break;
            var m=shown[i];
            lines.Add(new(i,PacketBoxScreenRules.Resolve(m.Raw,src.Names),y,PaletteColor(m.ColorIndex),PaletteColor(m.OutlineIndex)));
        }
    }
    static ushort PaletteColor(int index)=>index>=0&&index<RedNoticePanel.Palette.Length?RedNoticePanel.Palette[index]:RedNoticePanel.Palette[PacketBoxScreenRules.TextColorIndex];

    /// <summary>字寬：去掉色碼後的寬度（sub_F18AB0）。</summary>
    public static int Width(Graphics g,string raw)=>UiText.Measure(g,ServerMessageText.DisplayText(raw)).Width;

    public void Paint(UiCanvas canvas){
        var g=canvas.Graphics;
        foreach(var line in lines){
            int x=PacketBoxScreenRules.LineX(Width(g,line.Raw)),y=line.Y;
            var runs=RedNoticePanel.Runs(line.Raw,line.Color);var outline=NameColors.ToColor(line.Outline);
            var placed=new List<(RedNoticeRun Run,int X)>();int cx=x;
            foreach(var r in runs){placed.Add((r,cx));cx+=UiText.Measure(g,r.Text).Width;}
            foreach(var (r,rx) in placed){
                UiText.Draw(g,r.Text,rx,y-1,outline);UiText.Draw(g,r.Text,rx,y+1,outline);
                UiText.Draw(g,r.Text,rx-1,y,outline);UiText.Draw(g,r.Text,rx+1,y,outline);
            }
            foreach(var (r,rx) in placed)UiText.Draw(g,r.Text,rx,y,NameColors.ToColor(r.Color));
        }
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
}
