using System.Text.Json;
namespace P06;
public sealed record BonusStatsCommand(string Command,int Generation,int Epoch,int Revision,uint ActorId,string? Attribute){
    public static BonusStatsCommand Parse(JsonElement o){
        string cmd=o.GetProperty("cmd").GetString()??"";if(cmd is not ("bonusSelect" or "bonusSubmit" or "bonusRefresh"))throw new FormatException("bonus command");
        var expected=new List<string>{"v","id","cmd","generation","epoch","revision","actorId"};if(cmd!="bonusRefresh")expected.Add("attribute");
        if(!o.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(expected.Order())||o.GetProperty("v").GetInt32()!=1||o.GetProperty("id").GetInt32()<1)throw new FormatException("bonus fields");
        int gen=o.GetProperty("generation").GetInt32(),epoch=o.GetProperty("epoch").GetInt32(),rev=o.GetProperty("revision").GetInt32();uint own=o.GetProperty("actorId").GetUInt32();string? attr=cmd=="bonusRefresh"?null:o.GetProperty("attribute").GetString();
        if(gen<0||epoch<0||rev<0||own==0||cmd!="bonusRefresh"&&(attr==null||!BonusStatsRules.Valid(attr)))throw new FormatException("bonus identity/attribute");return new(cmd,gen,epoch,rev,own,attr);
    }
}
public sealed partial class ClientForm {
    readonly Panel bonusStatsPanel=new(){Visible=false,BackColor=Color.FromArgb(23,38,41)};
    readonly FlowLayoutPanel bonusStatsChoices=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    readonly Label bonusStatsNotice=new(){Dock=DockStyle.Bottom,Height=110,ForeColor=Color.White,Padding=new(6)};
    readonly Label bonusStatsIntro=new(){Dock=DockStyle.Top,Height=62,ForeColor=Color.White,Padding=new(6),Text="能力點分配\n目前能力值含裝備與效果；基礎值上限由原服檢查。剩餘點數：未提供。"};
    readonly Button bonusStatsRefresh=new(){Dock=DockStyle.Top,Height=34,Text="查詢可分配狀態"};
    readonly Button bonusStatsSubmit=new(){Dock=DockStyle.Bottom,Height=36,Text="請先選擇屬性",Enabled=false};
    BonusStatsView? displayedBonusStats;string bonusStatsSignature="";
    static BonusStatsCommand BonusCommand(BonusStatsView v,string cmd,string? key=null)=>new(cmd,v.Generation,v.Epoch,v.Revision,v.ActorId,key);
    void InitBonusStats(){
        bonusStatsPanel.Controls.AddRange([bonusStatsChoices,bonusStatsNotice,bonusStatsSubmit,bonusStatsRefresh,bonusStatsIntro]);
        bonusStatsPanel.Enter+=(_,_)=>LeaveGameFocus("bonus-stats");
        bonusStatsRefresh.Click+=async(_,_)=>{if(displayedBonusStats is {} v)await RunBonusStats(BonusCommand(v,"bonusRefresh"));};
        bonusStatsSubmit.Click+=async(_,_)=>{if(displayedBonusStats is {Selected:not null} v)await RunBonusStats(BonusCommand(v,"bonusSubmit",v.Selected));};
    }
    async Task<bool> RunBonusStats(BonusStatsCommand command){
        ClearInput("bonus-stats");bool accepted=false;
        if(!World.Walking)accepted=command.Command=="bonusSelect"?login.SelectBonusAttribute(command.Attribute!,command.Generation,command.Epoch,command.Revision,command.ActorId):await login.SendBonusStats(command.Command=="bonusRefresh",command.Attribute,command.Generation,command.Epoch,command.Revision,command.ActorId);
        SyncBonusStats();lastWebState="";PublishWebState();return accepted;
    }
    async Task DispatchBonusStats(int id,BonusStatsCommand command){bool accepted=await RunBonusStats(command);Reply(id,accepted?login.World.BonusStatsNotice:"能力點操作已過期、狀態未就緒或忙碌；未送出。");PublishWebState();}
    void SyncBonusStats(){
        var v=login.BonusStatsSnapshot();bonusStatsPanel.Visible=normalProduct&&login.State==LoginState.InWorld;displayedBonusStats=v;SyncNativeStatus(v);
        bonusStatsNotice.Text=v.Notice;bonusStatsRefresh.Enabled=v.CanRefresh&&!World.Walking;bonusStatsSubmit.Enabled=v.CanSubmit&&!World.Walking;
        bonusStatsSubmit.Text=v.Selected is {} key?"確認將「"+BonusStatsRules.Labels[Array.IndexOf(BonusStatsRules.Keys,key)]+"」提昇１點":"請先選擇屬性";
        string signature=JsonSerializer.Serialize(v)+World.Walking;if(signature==bonusStatsSignature)return;bonusStatsSignature=signature;
        foreach(Control old in bonusStatsChoices.Controls.Cast<Control>().ToArray())old.Dispose();bonusStatsChoices.Controls.Clear();
        foreach(var attr in v.Attributes){var command=BonusCommand(v,"bonusSelect",attr.Key);var button=new Button{Width=360,Height=34,Text=$"{(attr.Selected?"已選擇：":"選擇：")}{attr.Label} · 目前 {attr.Value?.ToString()??"未知"}",Enabled=v.CanSelect&&!World.Walking};button.Click+=async(_,_)=>await RunBonusStats(command);bonusStatsChoices.Controls.Add(button);}
    }
    // T328 (#766) native routing only: with the native UI on, the ReNStatusUIEx status window gets the same snapshot, and its
    // stat Up button runs the existing bonusSelect (local choice, no packet; the submit stays on the WinForms panel below).
    // Native UI off (--classic-ui) or the window unavailable/faulted → nothing changes; the WinForms panel is always kept.
    // R7-I (#811): the StatInfoUI window reads its row values from the same view (StatInfoPanel.BonusValue; rows without a
    // known source stay blank). Menu button 2 / Ctrl+A open the status window (MenuButtonsPanel); nothing changes here for that.
    void SyncNativeStatus(BonusStatsView v){
        if(!NativeUiActive||NativeUiRoot is not {} root||root.Find(P06.NativeUi.RenewalStatusPanel.PanelId) is not P06.NativeUi.RenewalStatusPanel panel||root.Faulted.Contains(panel))return;
        panel.StatPointUp??=key=>{if(displayedBonusStats is {} cur&&BonusStatsRules.Valid(key))_=RunBonusStats(BonusCommand(cur,"bonusSelect",key));};
        panel.Show(P06.NativeUi.RenewalStatusPanel.ViewOf(login.World.Status,v.CanSelect&&!World.Walking,v.Selected));
        if(root.Find(P06.NativeUi.StatInfoPanel.PanelId) is P06.NativeUi.StatInfoPanel info)info.ValueProvider??=StatInfoValues(panel);
    }
    /// <summary>R7-I (#811): StatInfoPanel.ValueProvider over the status window's latest view (read at paint time).</summary>
    internal static Func<string,string?> StatInfoValues(P06.NativeUi.RenewalStatusPanel status)=>name=>P06.NativeUi.StatInfoPanel.BonusValue(name,status.View);
}
