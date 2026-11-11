using System.Globalization;
using P06.NativeUi;
namespace P06;

// M7-X（T224／#531）：死亡統計窗 UserPlayStatsUI 與死亡逾時離線。依據 docs/reference/world-objects.md 第 6 節。
// 已驗證（WO§6）：自身死亡處理 sub_8C4EE0（FO 0x4C42E0）顯示 UserPlayStatsUI（地圖 2600 延後 5000 ms），
//   啟動計時器 0x29A：一般 501,264 ms，地圖 1017–1023 為 1,801,234 ms；到期（0x8BEEA9／FO 0x4BE2A9）仍死亡就送 C_QUIT 並關窗。
// 線上 opcode：TW16 C_QUIT(68) 對應 3.81 伺服器 C122 QUITGAME（C_Disconnect，無欄位；gap-matrix 第 2 節、SP C122）。
public static class DeathTimeoutRules {
    public const int QuitTimeoutMs=501264;
    public const int LongQuitTimeoutMs=1801234;
    public const int DelayedWindowMs=5000;
    public const ushort DelayedWindowMap=2600;
    public const byte QuitOpcode=122;
    public static int QuitTimeoutFor(ushort? map)=>map is >=1017 and <=1023?LongQuitTimeoutMs:QuitTimeoutMs;
    public static int WindowDelayFor(ushort? map)=>map==DelayedWindowMap?DelayedWindowMs:0;
    public static byte[] Quit()=>LoginPackets.Pad([QuitOpcode]);
}

public sealed record DeathView(string Phase,ushort? Map,long? DiedAt,long? WindowAt,long? QuitAt,long? RemainingMs,bool WindowOpen,bool Dismissed,bool QuitSent);

/// <summary>死亡計時的純狀態機；時間由呼叫端傳入，不讀時鐘、不送封包。</summary>
public sealed class DeathWatch {
    ushort? map;long? diedAt;bool dismissed,quitSent;
    public bool Active=>diedAt!=null;
    /// <summary>死亡起點：已有起點時不覆寫（同一次死亡只計一次）。</summary>
    public void Stamp(long now,ushort? deathMap){if(diedAt!=null)return;diedAt=now;map=deathMap;dismissed=false;quitSent=false;}
    public void Update(bool dead,long now,ushort? currentMap){if(!dead){Clear();return;}Stamp(now,currentMap);}
    public void Clear(){diedAt=null;map=null;dismissed=false;quitSent=false;}
    public long? WindowAt=>diedAt+DeathTimeoutRules.WindowDelayFor(map);
    public long? QuitAt=>diedAt+DeathTimeoutRules.QuitTimeoutFor(map);
    public bool WindowOpen(long now)=>diedAt!=null&&!dismissed&&!quitSent&&now>=WindowAt;
    public bool QuitDue(long now)=>diedAt!=null&&!quitSent&&now>=QuitAt;
    /// <summary>取消／關閉鈕：只關窗，計時照走（原版計時器與視窗分開）。</summary>
    public bool Dismiss(){if(diedAt==null||dismissed)return false;dismissed=true;return true;}
    public bool Reopen(){if(diedAt==null||!dismissed||quitSent)return false;dismissed=false;return true;}
    public void MarkQuitSent(){if(diedAt!=null)quitSent=true;}
    public DeathView View(long now)=>diedAt==null?new("alive",null,null,null,null,null,false,false,false)
        :new(quitSent?"quit-sent":dismissed?"dismissed":now<WindowAt?"window-delayed":"window-open",map,diedAt,WindowAt,QuitAt,Math.Max(0,QuitAt!.Value-now),WindowOpen(now),dismissed,quitSent);
}

// R2-D（T254／#615）PBW-B：原生 QUIT_WINDOW（PBW§4，已驗證）。死亡逾時（計時器 0x29A）送 C_QUIT 後關死亡窗、顯示訊息 47、開此窗。
//   重新開始 sub_981490：死亡→C_DEAD_RESTART（TW16 58＝3.81 C177）；存活→死亡、安全區或離上次戰鬥 ≥15 秒才送 C_RESTART（194＝C7），否則訊息 130。
//   離開遊戲 sub_981150：同樣 15 秒檢查（否則訊息 131），送 C_QUIT（68＝C122）後 exit(0)。取消／關閉 sub_A59AD0：只關窗。
public static class QuitWindowRules {
    public const int CombatHoldMs=15000;
    public const int DisconnectedMessage=47,RestartCombatMessage=130,QuitCombatMessage=131;
    public enum RestartKind { DeadRestart, CharacterSelect, CombatBlocked, Offline }
    public enum QuitKind { Quit, CombatBlocked, Exit }
    /// <summary>
    /// 原版 15 秒戰鬥限制。死亡處理把時間戳往前推 15000（WO§6），所以死亡時一定允許。
    /// 引擎沒有原版「上次戰鬥時間」的對應值（lastCombatAt＝null）：不推算，交原服判定（允許）。
    /// </summary>
    public static bool CombatAllows(bool dead,bool safety,long? lastCombatAt,long now)=>dead||safety||lastCombatAt is not long at||now-at>=CombatHoldMs;
    public static RestartKind Restart(bool inWorld,bool dead,bool combatAllows)=>!inWorld?RestartKind.Offline:dead?RestartKind.DeadRestart:combatAllows?RestartKind.CharacterSelect:RestartKind.CombatBlocked;
    public static QuitKind Quit(bool inWorld,bool combatAllows)=>!inWorld?QuitKind.Exit:combatAllows?QuitKind.Quit:QuitKind.CombatBlocked;
    /// <summary>字串表訊息；沒有字串表時用 PBW§4 記錄的原文（47「斷線。」、130、131）。</summary>
    public static string Message(int id){
        var r=ServerMessageText.Decode((ushort)id,[]);
        if(r.Recognized&&r.Displayed)return r.Text;
        return id switch{DisconnectedMessage=>"斷線。",RestartCombatMessage=>"戰鬥中，無法重新開始。",QuitCombatMessage=>"戰鬥中無法離開遊戲。",_=>r.Text};
    }
}

/// <summary>QUIT_WINDOW 的內容。Revision 每次開窗遞增；InWorld＝連線仍在世界中（逾時後為 false）。</summary>
public sealed record QuitWindowView(int Revision,bool InWorld,bool Dead,string Message);

/// <summary>UserPlayStatsUI 統計列名稱：字串 3123–3126 讀執行期 string-c.tbl（SS§4.1 已驗證同一張表）；表裡沒有時用 WO§6 的名稱。</summary>
public static class UserPlayStatsText {
    // WO§6 names the four rows; used only when string-c.tbl is unavailable or shorter than the id.
    static readonly Dictionary<int,string> Fallback=new(){[3123]="經驗",[3124]="擊殺數",[3125]="金幣",[3126]="取得物品"};
    public const int ExpRow=3123,KillRow=3124,AdenaRow=3125,ItemRow=3126,NoItemsMessage=3127,NewItemTooltip=3131,MinuteSuffix=1587,UnlimitedText=3344;
    /// <summary>sub_C0BB60：分鐘 ≥ 10,000,000 改寫字串 3344。</summary>
    public const int UnlimitedMinutes=10_000_000;
    public static (string Text,bool FromTable) Label(int? stringId,string rowName){
        if(stringId is not int id)return (rowName,false);
        if(ServerMessageText.Table is {} t&&id<t.Count){string s=ServerMessageText.DisplayText(t[id]).Trim();if(s.Length>0)return (s,true);}
        return (Fallback.TryGetValue(id,out var n)?n:$"#{id}",false);
    }
    // T272（#649）：3127／3131／1587／3344 從 string-c.tbl 取字（SS§4.1）；沒有字串表時用 SS§4.1 表列的本機原文。
    static readonly Dictionary<int,string> TextFallback=new(){[NoItemsMessage]="沒有獲得的道具",[NewItemTooltip]="查看道具 變動詳細內容",[MinuteSuffix]="分",[UnlimitedText]="無限"};
    public static string Text(int id){
        if(ServerMessageText.Table is {} t&&id>=0&&id<t.Count){string s=ServerMessageText.DisplayText(t[id]).Trim();if(s.Length>0)return s;}
        return TextFallback.TryGetValue(id,out var n)?n:$"#{id}";
    }
    /// <summary>統計列數值（sub_C0B840）。經驗 "%.4f%%"；其餘 "%d" 經 sub_88EA30（沒查，推測加千分位）。沒有基準時回 null（畫面顯示「—」）。</summary>
    public static string? Value(UserPlayStatsValues? v,int? stringId){
        if(v==null)return null;
        var inv=CultureInfo.InvariantCulture;
        return stringId switch{
            ExpRow=>v.ExpGain is decimal e?e.ToString("0.0000",inv)+"%":null,
            KillRow=>v.Kills.ToString("#,0",inv),
            AdenaRow=>v.AdenaGain is long a?a.ToString("#,0",inv):null,
            ItemRow=>v.Baseline?v.NewItems.ToString("#,0",inv):null,
            _=>null};
    }
    /// <summary>地監時間（sub_C0BB60）："%d%s"＋字串 1587；≥ 10,000,000 時字串 3344。</summary>
    public static string DungeonTime(int minutes)=>minutes>=UnlimitedMinutes?Text(UnlimitedText):minutes.ToString(CultureInfo.InvariantCulture)+Text(MinuteSuffix);
}

/// <summary>
/// T272（#649）死亡統計窗的數值（SS§3.1、FU3§4.2）。ExpGain＝(等級×100＋經驗%) 差；AdenaGain＝金幣差；Kills 恆為 0（3.81 不送 S250／61）；
/// NewItems＝新物品清單筆數；Baseline＝已有登入背包基準。DungeonTimes＝最近一次 S250／159（null＝還沒收到）。
/// </summary>
public sealed record UserPlayStatsValues(decimal? ExpGain,long? AdenaGain,int Kills,int NewItems,bool Baseline,IReadOnlyList<PacketBoxMapTime>? DungeonTimes){
    /// <summary>T290：新物品清單（UserObtainUI 的列），依第一次記入的順序；NewItems＝筆數。</summary>
    public IReadOnlyList<UserObtainEntry> Obtained {get;init;}=[];
}
/// <summary>新物品清單的一列：Gain＝高於基準的數量（差額）；Name＝伺服器原字串（"$n" 未解析，已去掉尾端 " (數量)"）；Icon＝背包圖示 gfx。</summary>
public sealed record UserObtainEntry(uint Id,uint Gain,string Name,int Icon);

public static class UserPlayStatsRules {
    /// <summary>SS§3.1 sub_C0BA30：重設後停用 1000 ms（計時器 sub_C0AE50 再設回可用）。</summary>
    public const int ResetCooldownMs=1000;
    /// <summary>FU3§4.2 sub_C038D0 的名稱備援只在旗標 0x80 時成立（3.81 S_AddItem 的祝福 byte 封印時 +128，推測就是這一位）。</summary>
    public const byte NameFallbackFlag=0x80;
    /// <summary>"$26 (5)" → "$26"：去掉伺服器在名稱尾端加的 " (數量)"（與 ItemText.Name 相同規則）。</summary>
    public static string BareName(string name,uint count){string suffix=" ("+count+")";return name.EndsWith(suffix,StringComparison.Ordinal)?name[..^suffix.Length]:name;}
    /// <summary>FU3§4.2 / SS§3.1：統計窗顯示時送 TW16 op 10 子碼 9＝3.81 C63 `C(9)`（SS2§14），伺服器回 S250／159。</summary>
    public const byte DungeonTimeOpcode=63,DungeonTimeSubtype=9;
    public static byte[] DungeonTimeRequest()=>LoginPackets.Pad([DungeonTimeOpcode,DungeonTimeSubtype]);
    /// <summary>3.81 金幣是背包物品（與 Castle.cs CastleGoldHeld 同一判斷）。</summary>
    public static bool IsAdena(ushort description,ushort icon,string name)=>description==0&&icon==5&&(name=="$4"||name.StartsWith("$4 (",StringComparison.Ordinal));
}

public sealed record UserPlayStatsButton(string Name,int X,int Y,int? BaseSurf,string MouseEvent,int? HighlightSurf=null,int? PushSurf=null);
public sealed record UserPlayStatsRow(string Name,int? IconSurf,int IconY,int? StringId,int LabelWidth,int ValueWidth,int Height,int Gap,Color LabelColor,Color ValueColor);
/// <summary>執行期由原客戶端 Tile.pak 的 UserPlayUI.xml 解析；缺節點或屬性丟 UiLayoutException，不補預設座標。</summary>
/// <remarks>Reset＝Window 下的 Reset_Button（選用；T290）。</remarks>
public sealed record UserPlayStatsLayout(int? BaseSurf,int? FrameSurf,bool Toggle,UserPlayStatsButton Restart,UserPlayStatsButton Quit,UserPlayStatsButton Cancel,UserPlayStatsButton? Close,int ListX,int ListY,int RowHeight,IReadOnlyList<UserPlayStatsRow> Rows,int WindowX=0,int WindowY=0,int? Moveable=null,UserPlayStatsButton? Reset=null) {
    public const string FileName="UserPlayUI";
    public const string RootName="UserPlayStatsUI";
    public static UserPlayStatsLayout From(UiLayout layout){
        var root=layout.Root;
        if(root.Name!=RootName)throw new UiLayoutException(layout.FileName,root.Path,$"根節點應為 {RootName}");
        var window=root.Child("Window");
        UserPlayStatsButton? Find(string name,bool required){
            var matches=window.ChildrenNamed("Button").Where(b=>b.Attr("Name")==name).ToList();
            if(matches.Count>1)throw new UiLayoutException(layout.FileName,window.Path+"/Button[@Name="+name+"]","節點不唯一");
            if(matches.Count==0){if(required)throw new UiLayoutException(layout.FileName,window.Path+"/Button[@Name="+name+"]","缺少節點");return null;}
            var node=matches[0];
            return new(name,node.Int("X"),node.Int("Y"),FirstSurf(node,"BaseSurf"),node.RequiredAttr("MouseEvent"),FirstSurf(node,"HighlightSurf"),FirstSurf(node,"PushSurf"));
        }
        var list=window.ChildrenNamed("List").SingleOrDefault(l=>l.Attr("Name")=="UserPlayStats_List")
            ??throw new UiLayoutException(layout.FileName,window.Path+"/List[@Name=UserPlayStats_List]","缺少節點");
        var rows=list.ChildrenNamed("List").Select(row=>{
            var labels=row.ChildrenNamed("Label").ToList();
            if(labels.Count<2)throw new UiLayoutException(layout.FileName,row.Path,"統計列需要名稱與數值兩個 Label");
            var icon=row.ChildrenNamed("Image").FirstOrDefault();
            return new UserPlayStatsRow(row.RequiredAttr("Name"),icon==null?null:FirstSurf(icon,"Surf"),icon?.TryInt("Y")??0,labels[0].TryInt("String"),
                labels[0].Int("Width"),labels[1].Int("Width"),row.Int("Height"),row.TryInt("Gap")??0,ParseColor(labels[0]),ParseColor(labels[1]));
        }).ToArray();
        int rowHeight=rows.Length==0?0:rows[0].Height;
        return new(FirstSurf(window,"BaseSurf"),FirstSurf(window,"FrameSurf"),string.Equals(root.Attr("Toggle"),"true",StringComparison.OrdinalIgnoreCase),
            Find("Restart_Button",true)!,Find("Quit_Button",true)!,Find("Cancel_Button",true)!,Find("CloseWindow_Button",false),list.Int("X"),list.Int("Y"),rowHeight,rows,window.Int("X"),window.Int("Y"),window.TryInt("Moveable"),Find("Reset_Button",false));
    }
    /// <summary>"5322;1" → 5322；原版解析只取第一個數字（V4 (b)）。沒有屬性或空值＝不畫。</summary>
    public static int? FirstSurf(UiNode node,string name){
        string? raw=node.Attr(name);if(raw==null||raw.Trim().Length==0)return null;
        string head=new(raw.Trim().TakeWhile(char.IsAsciiDigit).ToArray());
        if(head.Length==0||!int.TryParse(head,NumberStyles.None,CultureInfo.InvariantCulture,out int v))throw new UiLayoutException(node.Layout.FileName,node.Path,$"屬性 {name}=\"{raw}\" 不是 surf 編號");
        return v;
    }
    static Color ParseColor(UiNode node){
        string? raw=node.Attr("FontColor");if(raw==null)return Color.White;
        var parts=raw.Split(',');
        if(parts.Length!=3||parts.Any(p=>!byte.TryParse(p.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out _)))throw new UiLayoutException(node.Layout.FileName,node.Path,$"FontColor=\"{raw}\" 不是 R,G,B");
        return Color.FromArgb(byte.Parse(parts[0].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[1].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[2].Trim(),CultureInfo.InvariantCulture));
    }
}

public sealed partial class WorldSession {
    public bool RestartRequested {get;internal set;}
    /// <summary>自身 S85 復活次數（PBW§4：S_RESURRECT 時若 QUIT_WINDOW 開著就關掉）。不隨 Reset 歸零，只用來比對變化。</summary>
    public int OwnResurrections {get;private set;}
    bool recoveryAppearancePending;
    readonly DeathWatch deathWatch=new();
    internal Func<long> DeathClock=()=>Environment.TickCount64;
    internal void ResetRecovery(){RestartRequested=false;recoveryAppearancePending=false;deathWatch.Clear();}
    void CompleteRecoveryAppearance(){if(!recoveryAppearancePending)return;recoveryAppearancePending=false;SelfAppearancePending=false;}
    bool ReceiveRecovery(byte opcode,LoginReader r){
        if(opcode!=85)return false;
        uint id=r.D();byte type=r.C();r.D();uint gfx=r.D();r.Finish();
        if(type!=0||gfx>ushort.MaxValue){Unhandled(opcode);return true;}
        if(Actor?.ObjectId==id&&OwnDead){
            CancelPickupPresentation("resurrection");pendingAction=null;OwnCombat=null;OwnDead=false;RestartRequested=false;
            OwnResurrections++;
            // WO§4／§6: sub_A43B50 skips own appearance changes while a ghost (R2-D: S85 is a caller).
            if(!GhostSkipsSelfAppearance(id)){Actor=Actor with{Gfx=(ushort)gfx};AppearanceRevision++;}
            // S85 has no weapon or HP. Wait for S119 and positive authoritative HP.
            recoveryAppearancePending=true;SelfAppearancePending=true;
            // Original S_RESURRECT closes UserPlayStatsUI (WO§6); the timer stops with the death.
            deathWatch.Clear();
            CombatNotice="收到復活回報；等待角色外觀與生命值同步。";
        }else UpdateObject(id,o=>o.Dead?o with{Dead=false,Combat=null,ReceivedAction=null,Gfx=(ushort)gfx,MotionAt=0}:o);
        return true;
    }
    // Stamp the death moment from the S158 action 8 that Combat turns into OwnDead (observers run before core dispatch).
    [WorldReceive(158,Observe=true)]
    void ObserveOwnDeathAction(byte[] plain){
        if(plain.Length<6||plain[5]!=8||OwnDead||Actor is not {} own||BitConverter.ToUInt32(plain,1)!=own.ObjectId)return;
        deathWatch.Stamp(DeathClock(),MapId);
    }
    /// <summary>以目前 OwnDead 同步計時；漏掉 S158 時以第一次觀察到死亡為起點。</summary>
    internal void TrackDeath()=>deathWatch.Update(OwnDead,DeathClock(),MapId);
    internal DeathView DeathSnapshot(){TrackDeath();return deathWatch.View(DeathClock());}
    internal bool DeathQuitDue{get{TrackDeath();return OwnDead&&deathWatch.QuitDue(DeathClock());}}
    internal void DeathQuitSent(){deathWatch.MarkQuitSent();CombatNotice="死亡後逾時未重新開始，已依原版送出離線。";}
    internal bool DismissDeathWindow(){TrackDeath();return deathWatch.Dismiss();}
    internal bool ReopenDeathWindow(){TrackDeath();return OwnDead&&deathWatch.Reopen();}
    // ---- T272（#649）死亡統計的基準值（SS§3.1 sub_C075B0、FU3§4.2 UserObtainUI）----
    // 進入世界後第一個 S8 記等級與經驗%（原版 S_STATUS 每次都呼叫，只在旗標為假時記一次）。WorldSession.Reset 在每次進入世界時跑。
    // 金幣：3.81 的金幣是背包物品，不在 S8；改以登入時整批背包（S5）裡的金幣當基準（推測的對應，原版取 S_STATUS 的 +16）。
    // 獲得道具：S5 建基準清單；S15 數量高於基準記差額；S24 數量回到基準以下移除、否則更新差額；S57 移除。
    //   S15 先用物品 id 比對；T290（#698）補上 sub_C038D0 的備援：基準沒有這個 id 時，用「名稱（去掉尾端數量）＋第 6 參數」找
    //   基準清單，旗標 0x80 成立才沿用該筆的基準數量。第 6 參數＝S15 的第 6 個欄位（祝福 byte），旗標取同一個 byte 的 0x80
    //   （3.81 S_AddItem 封印時 +128）——這個對應是推測（FU3§9：第 6 參數沒追到封包欄位）。
    // 這些都用 M7-A 觀察器（在核心處理前、看同一個封包），自己讀欄位；格式錯誤時不動狀態，交給核心處理器拒絕。
    byte? statsBaseLevel;decimal? statsBaseExp;bool statsExpBaseline,statsInventoryBaseline;long statsBaseAdena;
    readonly Dictionary<uint,uint> statsBaseItems=[],statsNewItems=[];
    readonly Dictionary<(string Name,byte Bless),uint> statsBaseNamed=[];
    // 新物品清單的顯示資料（S15 當下的名稱與圖示；背包仍有時以背包為準）與第一次記入的順序（原版 vector 依加入順序）。
    readonly Dictionary<uint,(string Name,int Icon,long Order)> statsNewItemInfo=[];long statsNewItemOrder;
    [WorldReset] void ResetUserPlayStats(){statsBaseLevel=null;statsBaseExp=null;statsExpBaseline=false;statsInventoryBaseline=false;statsBaseAdena=0;statsBaseItems.Clear();statsBaseNamed.Clear();statsNewItems.Clear();statsNewItemInfo.Clear();}
    /// <summary>
    /// T290 重設鈕（SS§3.1 sub_C0BA30）：清空新物品清單、殺怪數歸 0（本來就恆為 0）、重建基準（sub_C0A800：等級、經驗%、金幣取目前值）。
    /// 基準清單不動（FU3§4.2：只在整批背包封包時建立）。原版會送的 op 55／44 在 3.81 被忽略，不送。回傳 false＝沒有任何基準可重建。
    /// </summary>
    internal bool ResetUserPlayStatsBaseline(){
        statsNewItems.Clear();statsNewItemInfo.Clear();
        bool any=false;
        if(Experience is {Percent:decimal cur} e){statsBaseLevel=e.Level;statsBaseExp=cur;statsExpBaseline=true;any=true;}
        else{statsBaseLevel=null;statsBaseExp=null;statsExpBaseline=false;}   // 沒有目前經驗：下一個 S8 重新記
        if(StatsInventoryBaseline){statsBaseAdena=CastleGoldHeld();statsInventoryBaseline=true;any=true;}
        return any;
    }
    [WorldReceive(8,Observe=true)]
    void ObserveStatsBaseline(byte[] plain){
        if(statsExpBaseline)return;
        try{var r=new LoginReader(plain);r.C();r.D();byte level=r.C();uint exp=r.D();statsExpBaseline=true;statsBaseLevel=level;statsBaseExp=DecodeExperience(level,exp,"S8").Percent;}
        catch(Exception e) when(e is ProtocolException or System.Text.DecoderFallbackException){}
    }
    sealed record ObservedStatsItem(uint Id,uint Count,bool Adena,string Name,ushort Icon,byte Bless);
    static ObservedStatsItem ObservedItem(LoginReader r){
        uint id=r.D();ushort desc=r.H();r.C();r.C();ushort icon=r.H();byte bless=r.C();uint count=r.D();r.C();string name=r.S();
        int status=r.C();for(int i=0;i<status;i++)r.C();for(int i=0;i<11;i++)r.C();
        return new(id,count,UserPlayStatsRules.IsAdena(desc,icon,name),name,icon,bless);
    }
    [WorldReceive(5,Observe=true)]
    void ObserveObtainBaseline(byte[] plain){
        var items=new List<ObservedStatsItem>();
        try{var r=new LoginReader(plain);r.C();int count=r.C();for(int i=0;i<count;i++)items.Add(ObservedItem(r));r.Finish();}catch(Exception e) when(e is ProtocolException or System.Text.DecoderFallbackException){return;}
        // sub_C03720：每筆加進基準清單（同 id 以新數量為準）；金幣基準＝這份清單的金幣。名稱＋第 6 參數另建一份給 S15 備援用。
        long adena=0;
        foreach(var i in items){statsBaseItems[i.Id]=i.Count;statsBaseNamed[(UserPlayStatsRules.BareName(i.Name,i.Count),i.Bless)]=i.Count;if(i.Adena)adena+=i.Count;}
        statsBaseAdena=statsInventoryBaseline?statsBaseAdena:adena;statsInventoryBaseline=true;
    }
    [WorldReceive(15,Observe=true)]
    void ObserveObtainAdd(byte[] plain){
        ObservedStatsItem item;
        try{var r=new LoginReader(plain);r.C();item=ObservedItem(r);r.Finish();}catch(Exception e) when(e is ProtocolException or System.Text.DecoderFallbackException){return;}
        ObtainAdded(item.Id,item.Count,UserPlayStatsRules.BareName(item.Name,item.Count),item.Bless,NativeUi.ItemIconSource.IconOf(item.Icon,item.Bless));
    }
    [WorldReceive(24,Observe=true)]
    void ObserveObtainChange(byte[] plain){
        uint id,count;
        try{var r=new LoginReader(plain);r.C();id=r.D();r.S();count=r.D();int n=r.C();for(int i=0;i<n;i++)r.C();r.Finish();}catch(Exception e) when(e is ProtocolException or System.Text.DecoderFallbackException){return;}
        ObtainChanged(id,count);
    }
    [WorldReceive(57,Observe=true)]
    void ObserveObtainRemove(byte[] plain){
        uint id;try{var r=new LoginReader(plain);r.C();id=r.D();r.Finish();}catch(Exception e) when(e is ProtocolException or System.Text.DecoderFallbackException){return;}
        RemoveObtained(id);
    }
    void SetObtained(uint id,uint gain){
        statsNewItems[id]=gain;
        if(!statsNewItemInfo.ContainsKey(id))statsNewItemInfo[id]=("",0,++statsNewItemOrder);
    }
    void RemoveObtained(uint id){statsNewItems.Remove(id);statsNewItemInfo.Remove(id);}
    /// <summary>
    /// sub_C038D0（S15）：基準有同 id→數量 ≥ 新數量不計、較少記差額；基準沒有這個 id→用名稱＋第 6 參數找基準，旗標 0x80 成立時沿用
    /// 該筆基準數量（同上比較）；都沒有→記全部數量。name 是去掉尾端數量的伺服器名稱；bless＝第 6 參數；icon 只用來顯示。
    /// </summary>
    internal void ObtainAdded(uint id,uint count,string name="",byte bless=0,int icon=0){
        if(id==0||count==0)return;
        uint? baseCount=statsBaseItems.TryGetValue(id,out uint b)?b
            :(bless&UserPlayStatsRules.NameFallbackFlag)!=0&&name.Length>0&&statsBaseNamed.TryGetValue((name,bless),out uint nb)?nb:null;
        if(baseCount is uint bc){if(bc>=count)return;SetObtained(id,count-bc);}
        else SetObtained(id,count);
        if(name.Length>0||icon>0)statsNewItemInfo[id]=(name,icon,statsNewItemInfo[id].Order);
    }
    /// <summary>sub_C04B00（S24）：新數量 ≤ 基準→移除；新清單已有→改差額；只有基準→新增差額；兩邊都沒有→不動。</summary>
    internal void ObtainChanged(uint id,uint count){
        bool hasBase=statsBaseItems.TryGetValue(id,out uint b);
        if(count<=b){RemoveObtained(id);return;}
        if(statsNewItems.ContainsKey(id)||hasBase)SetObtained(id,count-b);
    }
    bool StatsInventoryBaseline=>statsInventoryBaseline||InventoryLoaded;
    /// <summary>UserObtainUI 的列：背包裡還有這個物品時用背包的名稱與圖示，否則用 S15 當下記下的。</summary>
    IReadOnlyList<UserObtainEntry> ObtainedEntries()=>statsNewItems.Select(kv=>{
        var info=statsNewItemInfo.GetValueOrDefault(kv.Key);
        var entry=inventory.TryGetValue(kv.Key,out var item)
            ?new UserObtainEntry(kv.Key,kv.Value,UserPlayStatsRules.BareName(item.Name,item.Count),NativeUi.ItemIconSource.IconOf(item.IconId,item.Bless))
            :new UserObtainEntry(kv.Key,kv.Value,info.Name??"",info.Icon);
        return (entry,info.Order);
    }).OrderBy(x=>x.Order).Select(x=>x.entry).ToArray();
    /// <summary>目前的死亡統計值；沒有基準的欄位為 null。</summary>
    internal UserPlayStatsValues UserPlayStats(){
        decimal? exp=statsExpBaseline&&statsBaseLevel is byte bl&&statsBaseExp is decimal be&&Experience is {Percent:decimal cur} e?(e.Level*100m+cur)-(bl*100m+be):null;
        bool items=StatsInventoryBaseline;
        return new(exp,items?CastleGoldHeld()-statsBaseAdena:null,0,statsNewItems.Count,items,MapTimes){Obtained=ObtainedEntries()};
    }
    [WorldSnapshot("userPlayStats")]
    object UserPlayStatsSnapshotModel(){var v=UserPlayStats();return new{expGain=v.ExpGain,adenaGain=v.AdenaGain,kills=v.Kills,newItems=v.NewItems,baseline=v.Baseline,baseLevel=statsBaseLevel,baseExp=statsBaseExp,baseAdena=statsBaseAdena,newItemIds=statsNewItems.Keys.Order().ToArray()};}
    [WorldSnapshot("death")]
    object DeathExtensionSnapshot(){var v=DeathSnapshot();return new{phase=v.Phase,map=v.Map,diedAt=v.DiedAt,windowAt=v.WindowAt,quitAt=v.QuitAt,remainingMs=v.RemainingMs,windowOpen=v.WindowOpen,dismissed=v.Dismissed,quitSent=v.QuitSent,restartRequested=RestartRequested};}
}

public sealed partial class LoginConnection {
    bool restartSending;
    int deathQuitSendingGeneration=-1;
    public bool CanRestart=>State==LoginState.InWorld&&World.OwnDead&&World.Actor!=null&&!World.RestartRequested&&!restartSending&&!World.TeleportPending&&sendMovementGate!=null;
    public async Task<bool> RestartAfterDeath(){
        if(!CanRestart)return false;
        int session=generation,epoch=World.WorldEpoch,appearance=World.AppearanceRevision;
        uint id=World.Actor!.ObjectId;var sender=sendMovementGate!;restartSending=true;
        bool Current()=>session==generation&&epoch==World.WorldEpoch&&appearance==World.AppearanceRevision&&World.Actor?.ObjectId==id;
        try{
            bool sent=await sender(LoginPackets.Pad([177]),()=>Current()&&State==LoginState.InWorld&&World.OwnDead&&!World.RestartRequested&&!World.TeleportPending,()=>{
                // Original restart closes UserPlayStatsUI before sending (WO§6, sub_C0BB20).
                World.RestartRequested=true;World.DismissDeathWindow();CancelMovementInput();World.CombatNotice="已要求死亡後重新開始，等待伺服器恢復；不會自動重送。";Set(State,World.CombatNotice);
            });
            return sent&&session==generation;
        }catch(Exception ex){if(Current()){Cancel();Set(LoginState.Faulted,"重新開始傳送失敗："+ex.Message);}return false;}
        finally{if(session==generation)restartSending=false;}
    }
    public DeathView DeathSnapshot()=>CaptureWorld(()=>State==LoginState.InWorld?World.DeathSnapshot():new DeathView("alive",null,null,null,null,null,false,false,false));
    /// <summary>T272：死亡統計窗的數值（世界鎖內複製）。不在世界中為 null。</summary>
    public UserPlayStatsValues? UserPlayStatsSnapshot()=>State==LoginState.InWorld?CaptureWorld(World.UserPlayStats):null;
    /// <summary>T290 重設鈕：在世界鎖內重建基準（不送封包；冷卻由呼叫端的視窗管）。不在世界中回 false。</summary>
    public bool ResetUserPlayStats()=>State==LoginState.InWorld&&CaptureWorld(World.ResetUserPlayStatsBaseline);
    bool statsWindowShown;int statsWindowGeneration=-1;
    /// <summary>
    /// T272（SS2§14）：死亡統計窗每次由關變開（vtable sub_C0AED0）送一次 C63 `C(9)`，伺服器回 S250／159 地監時間清單。
    /// op 55／41（SS§3.2：3.81 忽略）不送。呼叫端每次同步畫面時傳入目前是否開窗；回傳 true＝這次有送出。
    /// </summary>
    public async Task<bool> SyncUserPlayStatsWindow(bool open){
        int session=generation;
        if(statsWindowGeneration!=session){statsWindowGeneration=session;statsWindowShown=false;}
        bool rising=open&&!statsWindowShown;statsWindowShown=open;
        if(!rising||State!=LoginState.InWorld||sendMovementGate is not {} sender)return false;
        try{return await sender(UserPlayStatsRules.DungeonTimeRequest(),()=>session==generation&&State==LoginState.InWorld,()=>{})&&session==generation;}
        catch(Exception ex){if(session==generation){Cancel();Set(LoginState.Faulted,"地監時間查詢傳送失敗："+ex.Message);}return false;}
    }
    public bool DismissDeathWindow()=>State==LoginState.InWorld&&CaptureWorld(World.DismissDeathWindow);
    public bool ReopenDeathWindow()=>State==LoginState.InWorld&&CaptureWorld(World.ReopenDeathWindow);
    /// <summary>死亡逾時：到期且仍死亡時送一次 C122（TW16 C_QUIT），送出後關閉連線。未到期或不在世界中回 false。</summary>
    public async Task<bool> CheckDeathTimeout(){
        int session=generation;
        bool Eligible()=>session==generation&&State==LoginState.InWorld&&sendMovementGate!=null&&CaptureWorld(()=>World.DeathQuitDue);
        if(deathQuitSendingGeneration==session||!Eligible())return false;
        var sender=sendMovementGate!;deathQuitSendingGeneration=session;
        try{
            bool sent=await sender(DeathTimeoutRules.Quit(),Eligible,()=>{CaptureWorld(()=>{World.DeathQuitSent();return true;});CancelMovementInput();});
            if(sent&&session==generation){
                // PBW§4: after C_QUIT the original closes the death window, shows message 47 and opens QUIT_WINDOW.
                string message=QuitWindowRules.Message(QuitWindowRules.DisconnectedMessage);
                Cancel();OpenQuitWindow(message,false);
                Set(LoginState.Disconnected,message+"（死亡後逾時未重新開始：已依原版送出離線 C122，連線已關閉。）");
            }
            return sent;
        }catch(Exception ex){if(session==generation){Cancel();Set(LoginState.Faulted,"死亡逾時離線傳送失敗："+ex.Message);}return false;}
        finally{if(deathQuitSendingGeneration==session)deathQuitSendingGeneration=-1;}
    }

    // QUIT_WINDOW state lives on the connection: the death-timeout path closes the world (Cancel → World.Reset) and the
    // window must stay. It belongs to one session generation; a new login (Run → Cancel) hides it.
    bool quitWindowOpen,quitWindowInWorld;int quitWindowGeneration=-1,quitWindowRevision,quitWindowResurrections;string quitWindowMessage="";
    bool quitSending;
    /// <summary>Only verified opener: the death timeout (PBW§4). inWorld=true is kept for a future ESC/menu entry (unverified).</summary>
    internal void OpenQuitWindow(string message,bool inWorld){
        quitWindowOpen=true;quitWindowInWorld=inWorld;quitWindowGeneration=generation;quitWindowRevision++;quitWindowMessage=message;
        quitWindowResurrections=World.OwnResurrections;
    }
    public void CloseQuitWindow(){quitWindowOpen=false;}
    public QuitWindowView? QuitWindowSnapshot(){
        if(!quitWindowOpen)return null;
        if(generation!=quitWindowGeneration){quitWindowOpen=false;return null;}
        bool inWorld=State==LoginState.InWorld;
        // S_RESURRECT (sub_9496B0) closes an open QUIT_WINDOW on own resurrection.
        if(inWorld&&quitWindowInWorld&&World.OwnResurrections!=quitWindowResurrections){quitWindowOpen=false;return null;}
        return new(quitWindowRevision,inWorld,inWorld&&World.OwnDead,quitWindowMessage);
    }
    /// <summary>QUIT_WINDOW「離開遊戲」在世界中：送一次 C122（TW16 C_QUIT 68），送出後關閉連線。呼叫端之後結束程式（原版 exit(0)）。</summary>
    public async Task<bool> QuitGame(){
        int session=generation;
        bool Eligible()=>session==generation&&State==LoginState.InWorld&&sendMovementGate!=null&&!quitSending;
        if(!Eligible())return false;
        var sender=sendMovementGate!;quitSending=true;
        try{
            bool sent=await sender(DeathTimeoutRules.Quit(),()=>session==generation&&State==LoginState.InWorld,CancelMovementInput);
            if(sent&&session==generation){Cancel();quitWindowOpen=false;Set(LoginState.Disconnected,"已送出離開遊戲（C122），連線已關閉。");}
            return sent;
        }catch(Exception ex){if(session==generation){Cancel();Set(LoginState.Faulted,"離開遊戲傳送失敗："+ex.Message);}return false;}
        finally{quitSending=false;}
    }
}

public sealed partial class ClientForm {
    // UserPlayStatsUI. Layout and images are read at runtime from the original client (UserPlayUI.xml, surf);
    // without client resources the same XML-free fallback below is used and labelled as an engine panel.
    readonly Panel recoveryPanel=new(){Size=new(250,200),BackColor=Color.FromArgb(40,32,32),Visible=false};
    readonly Label recoveryNotice=new(){AutoSize=false,ForeColor=Color.White,BackColor=Color.Transparent};
    readonly Button restartButton=new(){Text="重新開始"},deathQuitButton=new(){Text="結束"},deathCancelButton=new(){Text="取消"},deathCloseButton=new(){Text="×",Visible=false};
    readonly Button deathReopenButton=new(){Text="死亡選單",Size=new(96,26),Visible=false};
    // T290：Reset_Button（XML 有時才顯示）；按下重建基準並停用 1000 ms（SS§3.1 sub_C0BA30）。
    readonly Button deathResetButton=new(){Text="重設",Visible=false};long deathResetReadyAt;
    readonly List<Label> deathStatLabels=[];readonly List<(Label Label,int? StringId)> deathValueLabels=[];
    readonly System.Windows.Forms.Timer deathTimer=new(){Interval=250};
    volatile UserPlayStatsLayout? deathLayout;volatile string deathLayoutNotice="正在讀取原客戶端 UserPlayUI.xml。";
    Dictionary<int,Bitmap>? deathSurfs;bool deathLayoutApplied,deathTicking;
    internal string? DeathLayoutError {get;private set;}
    bool nativeRecoveryShown;
    void InitRecovery(){
        recoveryPanel.Controls.AddRange([recoveryNotice,restartButton,deathQuitButton,deathCancelButton,deathCloseButton,deathResetButton]);Controls.Add(recoveryPanel);Controls.Add(deathReopenButton);
        // T433（#991，classic-ui-audit G1）：死亡窗與「死亡選單」鈕屬於場景，不讓 InitDevTools 搬進開發工具（--classic-ui 死亡時才看得到）。
        KeepOnScene(recoveryPanel,deathReopenButton);
        deathResetButton.Click+=(_,_)=>{
            long now=Environment.TickCount64;if(now<deathResetReadyAt)return;
            login.ResetUserPlayStats();deathResetReadyAt=now+UserPlayStatsRules.ResetCooldownMs;SyncRecovery();
        };
        restartButton.Click+=async(_,_)=>await DeathRestart();
        deathQuitButton.Click+=(_,_)=>DeathQuit();
        deathCancelButton.Click+=(_,_)=>DeathDismiss();
        deathCloseButton.Click+=(_,_)=>DeathDismiss();
        // Engine transition: the original re-opens the Toggle window by an unverified trigger.
        deathReopenButton.Click+=(_,_)=>{login.ReopenDeathWindow();SyncRecovery();};
        ApplyDeathFallbackLayout();
        _=Task.Run(LoadDeathLayout);
        deathTimer.Tick+=async(_,_)=>await TickDeath();
        FormClosed+=(_,_)=>deathTimer.Dispose();deathTimer.Start();
        InitResurrection();
    }
    void LoadDeathLayout(){
        try{
            var resources=UiResources.Shared;
            var layout=UserPlayStatsLayout.From(resources.Xml.Load(UserPlayStatsLayout.FileName));
            var surfs=new Dictionary<int,Bitmap>();
            foreach(int? id in new[]{layout.BaseSurf,layout.Restart.BaseSurf,layout.Quit.BaseSurf,layout.Cancel.BaseSurf,layout.Close?.BaseSurf,layout.Reset?.BaseSurf}.Concat(layout.Rows.Select(r=>r.IconSurf)))
                if(id is int surf&&!surfs.ContainsKey(surf)&&resources.Surfs.Get(surf) is {} s)surfs[surf]=new Bitmap(s.Bitmap); // Own a copy: catalog bitmaps may be evicted.
            deathSurfs=surfs;deathLayout=layout;deathLayoutNotice="";
        }catch(Exception ex){deathLayoutNotice="（引擎面板：原客戶端 UserPlayUI 不可用）";DeathLayoutError=ex.GetType().Name+": "+ex.Message;}
    }
    void ApplyDeathFallbackLayout(){
        recoveryPanel.Size=new(250,170);recoveryPanel.BackgroundImage=null;
        recoveryNotice.SetBounds(10,8,230,36);
        restartButton.SetBounds(66,50,118,24);deathQuitButton.SetBounds(66,78,118,24);deathCancelButton.SetBounds(66,106,118,24);
        foreach(var b in new[]{restartButton,deathQuitButton,deathCancelButton}){b.BackgroundImage=null;b.FlatStyle=FlatStyle.Standard;b.Text=b==restartButton?"重新開始":b==deathQuitButton?"結束":"取消";}
    }
    void ApplyDeathLayout(UserPlayStatsLayout layout){
        var surfs=deathSurfs??[];
        Bitmap? Surf(int? id)=>id is int s&&surfs.TryGetValue(s,out var b)?b:null;
        if(Surf(layout.BaseSurf) is {} back){recoveryPanel.BackgroundImage=back;recoveryPanel.BackgroundImageLayout=ImageLayout.None;recoveryPanel.Size=back.Size;}
        void Place(Button control,UserPlayStatsButton spec){
            control.Location=new(spec.X,spec.Y);
            if(Surf(spec.BaseSurf) is {} image){control.Size=image.Size;control.BackgroundImage=image;control.BackgroundImageLayout=ImageLayout.None;control.FlatStyle=FlatStyle.Flat;control.FlatAppearance.BorderSize=0;control.BackColor=Color.Transparent;control.Text="";}
            else control.Size=new(118,20);
        }
        Place(restartButton,layout.Restart);Place(deathQuitButton,layout.Quit);Place(deathCancelButton,layout.Cancel);
        if(layout.Close is {} close){Place(deathCloseButton,close);deathCloseButton.Visible=true;}
        if(layout.Reset is {} reset){Place(deathResetButton,reset);deathResetButton.Visible=true;}
        // Rows of UserPlayStats_List (Vertical list, Horizontal rows: icon, name, value). The list's exact flow rule is inferred.
        foreach(var l in deathStatLabels){recoveryPanel.Controls.Remove(l);l.Dispose();}deathStatLabels.Clear();deathValueLabels.Clear();
        for(int i=0;i<layout.Rows.Count;i++){
            var row=layout.Rows[i];int y=layout.ListY+i*layout.RowHeight,x=layout.ListX;
            if(Surf(row.IconSurf) is {} icon){var pic=new Label{Location=new(x,y+row.IconY),Size=icon.Size,Image=icon,BackColor=Color.Transparent};deathStatLabels.Add(pic);x+=icon.Width+row.Gap;}
            string name=UserPlayStatsText.Label(row.StringId,row.Name).Text;
            deathStatLabels.Add(new Label{Location=new(x,y),Size=new(row.LabelWidth,row.Height),Text=name,ForeColor=row.LabelColor,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft});
            // T272: values come from the client-side baseline (SS§3.1); "—" only while a baseline is missing.
            var value=new Label{Location=new(x+row.LabelWidth+row.Gap,y),Size=new(row.ValueWidth,row.Height),Text=UserPlayStatsPanel.ValuePlaceholder,ForeColor=row.ValueColor,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleRight};
            deathStatLabels.Add(value);deathValueLabels.Add((value,row.StringId));
        }
        recoveryPanel.Controls.AddRange(deathStatLabels.ToArray());
        recoveryNotice.SetBounds(10,layout.ListY+layout.Rows.Count*layout.RowHeight+4,Math.Max(10,recoveryPanel.Width-20),30);
    }
    async Task TickDeath(){
        if(deathTicking)return;deathTicking=true;
        try{if(await login.CheckDeathTimeout())ClearInput("death-timeout-quit");SyncRecovery();}
        finally{deathTicking=false;}
    }
    async Task DeathRestart(){classicMenuPlayStats=false;ClearInput("restart-after-death");await login.RestartAfterDeath();SyncRecovery();}
    // "結束" (MouseEvent=Quit): the original handler is not verified; it uses the existing logout path (no packet).
    void DeathQuit(){classicMenuPlayStats=false;ClearInput("death-quit");CancelLogin(login.SessionGeneration,false);SyncRecovery();}
    void DeathDismiss(){classicMenuPlayStats=false;login.DismissDeathWindow();SyncRecovery();}

    // T433（#991，G8）：classic 的選單 7／Ctrl+Q／Alt+X 統計窗。比照原生 MenuPlayStatsSource（UiClientBridge.cs）：手動開啟只多一個
    // 旗標；死亡中再按＝關閉死亡窗（Dismiss），否則切換旗標。手動開啟不送 C63（原生同樣只在死亡窗由關變開時送）。
    bool classicMenuPlayStats;
    internal bool ToggleClassicPlayStats(){
        if(NativeUiActive||login.State!=LoginState.InWorld)return false;
        var next=ClassicPlayStatsToggle(recoveryPanel.Visible,login.DeathSnapshot().WindowOpen);
        classicMenuPlayStats=next.Menu;
        if(next.DismissDeath)login.DismissDeathWindow();
        SyncRecovery();return true;
    }
    /// <summary>G8：目前顯示中→關（死亡窗開著就 Dismiss）；沒顯示→手動開。</summary>
    internal static (bool Menu,bool DismissDeath) ClassicPlayStatsToggle(bool shown,bool deathWindowOpen)=>shown?(false,deathWindowOpen):(true,false);
    /// <summary>G1／G8：WinForms 死亡（統計）窗是否顯示。原生面板可用（native）時一律不顯示。</summary>
    internal static bool ClassicRecoveryVisible(bool native,bool inWorld,bool deathWindowOpen,bool menuPlayStats)=>!native&&inWorld&&(deathWindowOpen||menuPlayStats);
    /// <summary>G1：「死亡選單」鈕（死亡窗被關掉後重開）。原生面板可用時不顯示；死亡窗本身顯示時也不重複顯示。</summary>
    internal static bool ClassicDeathReopenVisible(bool native,bool inWorld,bool dismissed,bool quitSent,bool ownDead,bool recoveryShown)=>
        !native&&inWorld&&dismissed&&!quitSent&&ownDead&&!recoveryShown;

    // R2-D (#615): with the native UI active, UserPlayStatsUI and QUIT_WINDOW are IUiPanels; the WinForms panel stays the
    // fallback when the native UI is off or the panel faulted.
    bool AttachNativeRecovery(){
        if(!NativeUiActive||nativeUiRoot is not {} root)return false;
        if(root.Find(QuitWindowPanel.PanelId) is QuitWindowPanel quit&&quit.Source==null)quit.Source=new QuitWindowSource(this);
        if(root.Find(UserPlayStatsPanel.PanelId) is not UserPlayStatsPanel stats||root.Faulted.Contains(stats))return false;
        stats.Source??=new UserPlayStatsSource(this);
        return true;
    }
    sealed class UserPlayStatsSource(ClientForm form):IUserPlayStatsSource {
        public UserPlayStatsView? Current=>form.login.State==LoginState.InWorld&&form.login.DeathSnapshot().WindowOpen?new(form.login.CanRestart,form.login.UserPlayStatsSnapshot()):null;
        public void Restart(){_=form.DeathRestart();}
        public void Quit()=>form.DeathQuit();
        public void Dismiss()=>form.DeathDismiss();
        public void ShowMessage(string text)=>form.SetSingleActionNotice(text);
        public void Reset()=>form.login.ResetUserPlayStats();
    }
    sealed class QuitWindowSource(ClientForm form):IQuitWindowSource {
        public QuitWindowView? Current=>form.login.QuitWindowSnapshot();
        public void Restart()=>form.QuitWindowRestart();
        public void Quit()=>form.QuitWindowQuit();
        public void Close()=>form.QuitWindowClose();
    }
    // QUIT_WINDOW actions shared by the native panel and the R5-I WinForms fallback (QuitFallbackUi.cs).
    void QuitWindowRestart(){
        var view=login.QuitWindowSnapshot();if(view==null)return;
        // Engine has no counterpart of the original last-combat timestamp: null → server decides (QuitWindowRules.CombatAllows).
        switch(QuitWindowRules.Restart(view.InWorld,view.Dead,QuitWindowRules.CombatAllows(view.Dead,false,null,Environment.TickCount64))){
            case QuitWindowRules.RestartKind.DeadRestart:login.CloseQuitWindow();_=DeathRestart();break;
            case QuitWindowRules.RestartKind.CharacterSelect:login.CloseQuitWindow();ClearInput("quit-window-restart");_=login.SwitchCharacter(login.SessionGeneration,login.Revision);break;
            case QuitWindowRules.RestartKind.CombatBlocked:SetSingleActionNotice(QuitWindowRules.Message(QuitWindowRules.RestartCombatMessage));break;
            // Engine transition: the connection is already closed (C122 sent), so C177 cannot be sent; go back to login.
            default:login.CloseQuitWindow();ClearInput("quit-window-relogin");CancelLogin(login.SessionGeneration,false);break;
        }
        SyncRecovery();
    }
    void QuitWindowQuit(){
        var view=login.QuitWindowSnapshot();if(view==null)return;
        switch(QuitWindowRules.Quit(view.InWorld,QuitWindowRules.CombatAllows(view.Dead,false,null,Environment.TickCount64))){
            case QuitWindowRules.QuitKind.Quit:_=QuitFromWindow();break;
            case QuitWindowRules.QuitKind.CombatBlocked:SetSingleActionNotice(QuitWindowRules.Message(QuitWindowRules.QuitCombatMessage));break;
            default:login.CloseQuitWindow();BeginInvoke(new Action(Close));break;   // original: exit(0)
        }
    }
    void QuitWindowClose(){login.CloseQuitWindow();SyncRecovery();}
    async Task QuitFromWindow(){ClearInput("quit-window-quit");if(await login.QuitGame()&&!IsDisposed)BeginInvoke(new Action(Close));}

    void SyncRecovery(){
        if(!deathLayoutApplied&&deathLayout is {} layout){deathLayoutApplied=true;try{ApplyDeathLayout(layout);}catch(Exception ex){deathLayoutNotice="（引擎面板：UserPlayUI 版面套用失敗）";DeathLayoutError=ex.GetType().Name+": "+ex.Message;ApplyDeathFallbackLayout();}}
        var view=login.DeathSnapshot();
        _=login.SyncUserPlayStatsWindow(login.State==LoginState.InWorld&&view.WindowOpen);
        bool native=AttachNativeRecovery();
        bool nativeShown=native&&(view.WindowOpen||login.QuitWindowSnapshot()!=null);
        if(nativeShown!=nativeRecoveryShown)Invalidate();
        nativeRecoveryShown=nativeShown;
        SyncQuitFallback();   // R5-I (#741): WinForms QUIT_WINDOW when no native panel is usable.
        bool inWorld=login.State==LoginState.InWorld;
        if(!inWorld||native)classicMenuPlayStats=false;
        recoveryPanel.Visible=ClassicRecoveryVisible(native,inWorld,view.WindowOpen,classicMenuPlayStats);
        deathReopenButton.Visible=ClassicDeathReopenVisible(native,inWorld,view.Dismissed,view.QuitSent,login.World.OwnDead,recoveryPanel.Visible);
        if(deathReopenButton.Visible){deathReopenButton.Location=new(Math.Max(0,(ClientSize.Width-deathReopenButton.Width)/2),160);deathReopenButton.BringToFront();}
        if(!recoveryPanel.Visible)return;
        // Screen position of the window is not in the XML (X=0,Y=0 is window-relative); centred like the previous panel.
        recoveryPanel.Location=new(Math.Max(0,(ClientSize.Width-recoveryPanel.Width)/2),160);
        recoveryNotice.Text=(login.World.RestartRequested?"已送出重新開始，等待伺服器恢復。":view.WindowOpen?"角色已死亡。":"遊戲統計（手動開啟）。")+(deathLayoutNotice.Length>0?"\n"+deathLayoutNotice:"");
        var stats=login.UserPlayStatsSnapshot();
        foreach(var (label,id) in deathValueLabels){string text=UserPlayStatsText.Value(stats,id)??UserPlayStatsPanel.ValuePlaceholder;if(label.Text!=text)label.Text=text;}
        restartButton.Enabled=login.CanRestart;deathResetButton.Enabled=Environment.TickCount64>=deathResetReadyAt;recoveryPanel.BringToFront();
    }
}
