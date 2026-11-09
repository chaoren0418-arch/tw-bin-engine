namespace P06.NativeUi;

// R3-B（T278／#671）：舊式寵物指令窗 PetControl（WIN§1.5；gap-matrix 第 1 節 op 22、第 4.2 節）。
// 來源：TW1608262200.bin SHA-256 E12949CF8912C5D62EF269BA02CA37EB5CABF8F379C3D5E10189279BF09641A2（ImageBase 0x400000，FO＝VA−0x401000+0x400）。
// 已驗證（靜態反組譯，報告 docs/reports/issue-671.md 第 2 節）：
//   - 建構子 sub_F3A0C0（FO 0xB394C0，sub_8C7C80 在 VA 0x8C8E9B 呼叫），全域 dword_1656908；按鈕容器 sub_8156B0(2, 0x23, −1)＝2 顆鈕。
//   - 第 0 鈕：sub_8FA710(1159, 886, (6,30), cb=0xF3AC80, idx 0)；第 1 鈕：sub_8FA710(1156, 885, (−31,20), idx 1)
//     （codepage 932 時第 1 鈕沒有回呼；TW 950 有）。第一個 surf 交給基底按鈕 sub_8F9EB0，第二個存在 +0x4C。
//   - 回呼 sub_F3AC80：idx 0＝開寵物背包窗（dword_1656930）並送 C_CHECK_INVENTORY（TW16 op 160，"cd" 寵物 id）；
//     idx 1＝寵物種類 ≠ 1 時送 C_HACTION（TW16 op 227，"cdss" 寵物 id、"attackchr"、""）並進入選目標模式（+0x20）。
//   - 開窗 sub_F3ABB0（vtable[8]）：種類 ＝ 1 時第 1 鈕停用；S39 處理器（VA 0x585BD8–0x585D6F）收到 anicom／moncom 時
//     設寵物 id（sub_F3B6A0，+0xC）與種類（sub_F3B6E0，+0x10：html＝"moncom" → 1，否則 0），再開 PetControl。
// 3.81 對應：op 227 ＝ C125 C_NPCAction（followups-3）→ 既有 PetPackets.TargetCommand；op 160 ＝ C99 C_Check，3.81 伺服器不回任何
//   封包（packetbox-windows §2），所以背包鈕改走 3.81 會回背包的 C103 C_PetMenu（既有 "refresh"）。
// 推測（標明）：按鈕平常畫第一個 surf、按下畫第二個 surf；容器原點（+0x14／+0x18，建構時 0，之後誰寫入沒查到）用
//   世界區中心（UiRoot.WorldRect）代替；停用的第 1 鈕照畫但不可點。
// 待驗證（不做）：淡入淡出（sub_F3B100 一帶以 alpha 0x1F 遞減）、按鈕的提示文字、Esc、寵物背包原生窗（dword_1656930，不在本組範圍）。

/// <summary>面板顯示的一次開窗：Revision 每收到一次 anicom／moncom 加一；Summon＝moncom（召喚物，第 1 鈕停用）。</summary>
public sealed record PetControlView(long Revision,uint PetId,bool Summon,bool CanSend,Point Anchor);

/// <summary>由 PetUi.cs 的 ClientForm 轉接；面板不碰 LoginConnection。</summary>
public interface IPetControlHost {
    PetControlView? Current {get;}
    /// <summary>第 0 鈕：要寵物背包（3.81 C103）。</summary>
    void Bag(PetControlView shown);
    /// <summary>第 1 鈕：指定攻擊目標（3.81 C125 attackchr），只對寵物。</summary>
    void Target(PetControlView shown);
}

/// <summary>一次 PetControl 開窗：寵物 id 與種類。</summary>
public sealed record PetControlOpen(long Revision,uint PetId,bool Summon);

/// <summary>
/// 把「原服又送了一次 anicom／moncom」轉成開窗事件：每隻夥伴的資訊物件換成新的一份（Pets.cs 每收到一次就建一個新的 PetInfo）就開一次。
/// 第一次觀察只記錄、不開窗（避免接上時把舊資訊當成新回覆）；離開世界、夥伴消失時關閉。
/// </summary>
public sealed class PetControlTracker {
    readonly Dictionary<uint,object> seen=[];
    bool primed;long revision;
    public PetControlOpen? Current{get;private set;}
    public PetControlOpen? Observe(bool inWorld,IEnumerable<(uint Id,object? Info,bool Summon)> companions){
        if(!inWorld){seen.Clear();primed=false;Current=null;return null;}
        var rows=companions.ToList();var ids=rows.Select(r=>r.Id).ToHashSet();
        foreach(uint id in seen.Keys.Where(k=>!ids.Contains(k)).ToArray())seen.Remove(id);
        foreach(var r in rows){
            if(r.Info==null){seen.Remove(r.Id);continue;}
            if(seen.TryGetValue(r.Id,out var old)&&ReferenceEquals(old,r.Info))continue;
            seen[r.Id]=r.Info;
            if(primed)Current=new(++revision,r.Id,r.Summon);
        }
        primed=true;
        if(Current is {} c&&!ids.Contains(c.PetId))Current=null;
        return Current;
    }
    public void Dismiss(long shownRevision){if(Current?.Revision==shownRevision)Current=null;}
}

public sealed class PetControlPanel:IUiPanel {
    [UiPanel(35)] internal static UiPanelRegistration Registration=>new(PetControlPanel.PanelId,PetControlPanel.LayoutName,_=>new PetControlPanel());   // R3-B (#671): PetControl (hard-coded, no XML)
    public const string PanelId="PET_CONTROL";
    public const string LayoutName="uiextra86";   // 舊式寫死窗，不讀 XML；只借既有版面檔名讓註冊表能建立（同 YesNoPanel）
    public const int BagSurf=1159,BagPushSurf=886,TargetSurf=1156,TargetPushSurf=885;
    public static readonly Point BagOffset=new(6,30),TargetOffset=new(-31,20);
    public const string BagKey="bag",TargetKey="target";

    public sealed record Button(string Key,Point Offset,int SurfId,UiSurface? Surface,UiSurface? Push);

    Func<int,UiSurface?> surf=_=>null;
    Button bag=null!,target=null!;
    bool usable;PetControlView? view;long dismissed=-1;string? pressed;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public bool Visible=>view!=null;
    public PetControlView? View=>view;
    internal bool Usable=>usable;
    internal string? Pressed=>pressed;
    public IPetControlHost? Host{get;private set;}
    public void Attach(IPetControlHost host)=>Host=host;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    internal void Load(Func<int,UiSurface?> surfs){
        surf=surfs;
        bag=new(BagKey,BagOffset,BagSurf,surf(BagSurf),surf(BagPushSurf));
        target=new(TargetKey,TargetOffset,TargetSurf,surf(TargetSurf),surf(TargetPushSurf));
        var missing=new[]{bag,target}.Where(b=>b.Surface==null).Select(b=>b.SurfId.ToString()).ToArray();
        usable=missing.Length==0;
        if(!usable)UiResourceLog.Write($"panel {PanelId} 停用：缺少按鈕底圖 surf {string.Join("、",missing)}");
    }

    public void Update(IUiGameState state){
        var now=usable&&state.InWorld?Host?.Current:null;
        if(now!=null&&now.Revision==dismissed)now=null;
        if(now?.Revision!=view?.Revision)pressed=null;
        view=now;
    }

    public Rectangle At(Button b){
        var a=view?.Anchor??Point.Empty;
        return b.Surface==null?Rectangle.Empty:new(a.X+b.Offset.X,a.Y+b.Offset.Y,b.Surface.Width,b.Surface.Height);
    }
    public Rectangle PartRect(string key)=>At(key==BagKey?bag:target);
    public Rectangle Bounds=>view==null?Rectangle.Empty:Rectangle.Union(At(bag),At(target));
    bool Enabled(Button b)=>view is {} v&&v.CanSend&&(b==bag||!v.Summon);
    internal string? HitKey(Point p){
        if(view==null)return null;
        foreach(var b in new[]{bag,target})if(Enabled(b)&&At(b).Contains(p))return b.Key;
        return null;
    }

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        foreach(var b in new[]{bag,target}){
            var s=pressed==b.Key&&b.Push!=null?b.Push:b.Surface;
            var r=At(b);canvas.DrawSurface(s,r.X,r.Y);
        }
    }

    void Fire(string key){
        if(view is not {} v)return;
        dismissed=v.Revision;view=null;pressed=null;   // 原版按下後轉入淡出（狀態 3）再關窗
        if(key==BagKey)Host?.Bag(v);else Host?.Target(v);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Button!=MouseButtons.Left&&e.Kind is UiMouseKind.Down or UiMouseKind.Up)return inside;
        switch(e.Kind){
            case UiMouseKind.Down:pressed=HitKey(e.Location);return inside;
            case UiMouseKind.Up:{
                string? was=pressed,key=HitKey(e.Location);pressed=null;
                if(was!=null&&was==key)Fire(key);
                return inside||was!=null;
            }
            default:return inside;
        }
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
