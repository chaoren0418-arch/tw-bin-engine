using P06.NativeUi;
namespace P06;

// R3-B（T278／#671）：原生 UI 關閉時的召喚怪物選擇暫行清單（gap-matrix 第 12.5 節；WIN§1.3）。
// 不是原版畫面：原版是 CSummonUI（NativeUi/SummonPanel.cs，只在原生 UI 下出現）。本清單放在 WinForms「寵物」分頁，
// 資料一樣讀原客戶端 Tile.pak 的 summonList.xml（SummonCatalog，不複製資源）；選定後走同一條 LoginConnection.SendSummonChoice
// （server：C125 D(自己) S(action)；local：技能 51 的 C6），宿主同樣以 revision 擋過期與重複送出。
// 等級規則同 SummonPanel：角色等級（原服 S8）≥ 怪物等級才可選；沒收到等級就全部不可選（不推定）。
// 怪物名稱（desc 字串表）待查證，所以只顯示召喚編號、需求等級與 XML 的 minCha。

public sealed record SummonFallbackEntry(SummonMonster Monster,bool Enabled);
public sealed record SummonFallbackGroup(int Level,SummonFallbackEntry[] Monsters);

public static class SummonFallback {
    /// <summary>依等級由高到低分組（同 SummonPanel 等級鈕的順序）；組內保持 summonList.xml 的順序。</summary>
    public static SummonFallbackGroup[] Groups(IReadOnlyList<SummonMonster> catalog,int? characterLevel)=>
        catalog.GroupBy(m=>m.Level).OrderByDescending(g=>g.Key)
            .Select(g=>new SummonFallbackGroup(g.Key,g.Select(m=>new SummonFallbackEntry(m,characterLevel is int lv&&lv>=m.Level)).ToArray())).ToArray();
    /// <summary>可送出：請求開著、宿主可送、等級足夠、怪物來自目前清單且 action 是召喚編號。</summary>
    public static bool CanChoose(SummonView view,IReadOnlyList<SummonMonster> catalog,SummonMonster monster)=>
        view.Open&&view.CanSend&&view.Level is int lv&&lv>=monster.Level&&catalog.Contains(monster)&&PetPackets.ValidSummonAction(monster.Action);
    public static string Caption(SummonMonster m)=>$"召喚編號 {m.Action} · 需等級 {m.Level} · minCha {m.MinCha}";
    /// <summary>新的 server 請求（S39 summonlist）才自動切到寵物分頁；同一個 revision 只切一次。</summary>
    public static bool ShouldReveal(SummonView view,bool nativeUi,long lastRevealed)=>!nativeUi&&view.Open&&view.Source=="server"&&view.Revision!=lastRevealed;
}

public sealed partial class ClientForm {
    IReadOnlyList<SummonMonster>? summonFallbackCatalog;
    string summonFallbackNotice="";
    int summonFallbackLoad;   // 0＝未讀、1＝讀取中、2＝完成（成功或失敗）
    long summonFallbackRevealed=-1;
    bool summonFallbackSending;

    object SummonFallbackSignature=>new{load=Volatile.Read(ref summonFallbackLoad),count=summonFallbackCatalog?.Count,notice=summonFallbackNotice,sending=summonFallbackSending};

    void EnsureSummonFallbackCatalog(){
        if(Interlocked.CompareExchange(ref summonFallbackLoad,1,0)!=0)return;
        summonFallbackNotice="正在讀取原客戶端 summonList.xml…";
        _ = Task.Run(()=>{
            try{summonFallbackCatalog=SummonCatalog.Parse(UiResources.Shared.Xml.Load(SummonCatalog.FileName));summonFallbackNotice="";}
            catch(Exception ex){summonFallbackNotice="原客戶端 summonList.xml 不可用，無法列出召喚怪物："+ex.GetType().Name+": "+ex.Message;}
            finally{Volatile.Write(ref summonFallbackLoad,2);}
        });
    }

    /// <summary>每個 timer tick：原生 UI 關閉且原服送來新的 summonlist 時，先讀清單並切到寵物分頁（不再只有提示）。</summary>
    void SyncSummonFallback(){
        if(NativeUiActive||login.State!=LoginState.InWorld)return;
        var v=login.SummonSnapshot();
        if(v.Open)EnsureSummonFallbackCatalog();
        if(!SummonFallback.ShouldReveal(v,NativeUiActive,summonFallbackRevealed))return;
        summonFallbackRevealed=v.Revision;
        ShowPlayerService("寵物");
    }

    void AddSummonFallback(SummonView summon,Action<string> label,Action<string,bool,Action> button){
        if(!summon.Open)return;
        EnsureSummonFallbackCatalog();
        label($"暫行召喚清單（原生 UI 關閉時使用；原版為 SummonUI 視窗）· 角色等級 {summon.Level?.ToString()??"未收到"} · 魅力 {summon.Charisma?.ToString()??"未收到"}");
        if(summonFallbackCatalog is not {} catalog){label(summonFallbackNotice.Length>0?summonFallbackNotice:"正在讀取原客戶端 summonList.xml…");return;}
        if(summonFallbackSending)label("召喚選擇送出中…");
        foreach(var group in SummonFallback.Groups(catalog,summon.Level)){
            label($"等級 {group.Level} 以上");
            foreach(var entry in group.Monsters){
                var monster=entry.Monster;
                button(SummonFallback.Caption(monster)+(entry.Enabled?"":"（等級不足）"),!summonFallbackSending&&SummonFallback.CanChoose(summon,catalog,monster),()=>ChooseSummonFallback(summon,monster));
            }
        }
    }

    void ChooseSummonFallback(SummonView shown,SummonMonster monster){
        var v=login.SummonSnapshot();
        if(summonFallbackSending||summonFallbackCatalog is not {} catalog||v.Revision!=shown.Revision||!SummonFallback.CanChoose(v,catalog,monster))return;
        ClearInput("summon-choice");
        _ = SendSummonFallback(v,monster.Action);
    }
    async Task SendSummonFallback(SummonView v,string action){
        summonFallbackSending=true;petUiSignature="";
        try{await login.SendSummonChoice(v,action);}
        finally{summonFallbackSending=false;petUiSignature="";SyncPetUi(this,EventArgs.Empty);}
    }
}
