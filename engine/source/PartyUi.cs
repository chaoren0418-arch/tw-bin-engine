namespace P06;

// Mounted by the HUD; all party-specific controls and connection access stay in this module.
public sealed class PartyUi:Panel {
    readonly FlowLayoutPanel rows=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    readonly FlowLayoutPanel actions=new(){Dock=DockStyle.Top,Height=36,WrapContents=false};
    readonly Label notice=new(){Dock=DockStyle.Bottom,Height=42,ForeColor=Color.White,AutoEllipsis=true};
    readonly System.Windows.Forms.Timer refresh=new(){Interval=200};
    readonly Button invite,list,leave,accept,reject;
    readonly CheckBox distribute=new(){Text="自動分配",Width=90,ForeColor=Color.White,AccessibleName="組隊自動分配模式"};
    readonly PlayerHudView hud;readonly Func<bool> isOpen;
    PartyView? shown;string rowKey="";bool sending;
    ClientForm? Owner=>hud.FindForm() as ClientForm;
    public static void Mount(PlayerHudView hud,FlowLayoutPanel menu,Action toggle,Func<bool> isOpen){
        var panel=new PartyUi(hud,isOpen);hud.Controls.Add(panel);
        var button=new Button{Text="隊伍",AccessibleName="隊伍面板",Width=58,Height=30,Margin=new(3,2,3,2),BackColor=PlayerHudPainter.Panel,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        menu.Controls.Add(button);button.Click+=(_,_)=>{toggle();panel.Sync();};
        hud.LayoutChanged+=panel.Sync;hud.SizeChanged+=(_,_)=>panel.Sync();
        hud.VisibleChanged+=(_,_)=>{if(hud.Visible)panel.refresh.Start();else panel.refresh.Stop();panel.Sync();};
    }
    PartyUi(PlayerHudView hud,Func<bool> isOpen){
        this.hud=hud;this.isOpen=isOpen;Visible=false;BackColor=PlayerHudPainter.Ink;AccessibleName="隊伍名稱與 HP";
        invite=Button("邀請選取玩家","invite",116);list=Button("更新名單","list",84);leave=Button("離開隊伍","leave",84);
        accept=Button("接受","accept",58);reject=Button("拒絕","reject",58);
        actions.Controls.AddRange([invite,distribute,list,leave,accept,reject]);Controls.AddRange([rows,notice,actions]);
        refresh.Tick+=(_,_)=>Sync();Enter+=(_,_)=>Owner?.PartyTakeFocus();
    }
    Button Button(string text,string command,int width){var b=new Button{Text=text,AccessibleName=text,Width=width,Height=30,ForeColor=Color.White,BackColor=PlayerHudPainter.Panel};b.Click+=async(_,_)=>await Send(command);return b;}
    async Task Send(string command,string name=""){
        if(sending||shown==null||Owner is not {} owner)return;
        sending=true;var stamp=shown;Sync();
        try{bool sent=await owner.PartyAction(command,stamp,name,distribute.Checked?(byte)1:(byte)0);if(!sent&&!IsDisposed)notice.Text="未送出：畫面資料已更新、狀態忙碌或目標不適用，請重新核對。";}
        finally{sending=false;if(!IsDisposed)Sync();}
    }
    void Sync(){
        if(IsDisposed)return;SetBounds(0,0,hud.Width,PlayerHudGeometry.DrawerHeight);Visible=hud.Visible&&isOpen();
        if(!Visible||Owner is not {} owner)return;BringToFront();shown=owner.PartyRead();var v=shown;
        bool ready=v.Available&&!sending;invite.Enabled=ready&&v.CanInvite;list.Enabled=ready;leave.Enabled=ready&&v.Members.Length>0;
        accept.Visible=reject.Visible=v.Invitation!=null;accept.Enabled=reject.Enabled=ready;distribute.Enabled=ready;
        notice.Text=v.Notice;notice.AccessibleName=v.Notice;
        string key=string.Join('|',v.Members.Select(m=>$"{m.ObjectId}:{m.Name}:{m.HpPercent}"))+v.CanKick+ready;
        if(key==rowKey)return;rowKey=key;
        foreach(Control old in rows.Controls.Cast<Control>().ToArray())old.Dispose();rows.Controls.Clear();
        foreach(var member in v.Members){
            var line=new FlowLayoutPanel{Width=Math.Max(240,Width-30),Height=36,WrapContents=false};
            line.Controls.Add(new Label{Text=member.Name,AccessibleName="隊員："+member.Name,Width=140,ForeColor=Color.White,AutoEllipsis=true});
            line.Controls.Add(new ProgressBar{Width=120,Height=22,Maximum=100,Value=member.HpPercent??0,AccessibleName=member.Name+" HP "+(member.HpPercent is byte hp?$"{hp}%":"未知")});
            line.Controls.Add(new Label{Text=member.HpPercent is byte ratio?$"{ratio}%":"HP 未知",Width=70,ForeColor=Color.White});
            var kick=new Button{Text="踢除",AccessibleName="踢除隊員："+member.Name,Width=58,Height=28,Enabled=ready&&v.CanKick};string name=member.Name;kick.Click+=async(_,_)=>await Send("kick",name);line.Controls.Add(kick);rows.Controls.Add(line);
        }
        if(v.Members.Length==0)rows.Controls.Add(new Label{Text="尚未收到隊員資料。",AutoSize=true,ForeColor=Color.Silver});
    }
    protected override void Dispose(bool disposing){if(disposing)refresh.Dispose();base.Dispose(disposing);}
}

public sealed partial class ClientForm {
    internal PartyView PartyRead()=>login.PartySnapshot();
    internal void PartyTakeFocus()=>LeaveGameFocus("party");
    internal Task<bool> PartyAction(string command,PartyView view,string name,byte mode){
        bool walking=World.Walking;ClearInput("party");
        return walking?Task.FromResult(false):login.SendParty(command,view,name,login.World.TargetId??0,mode);
    }
}
