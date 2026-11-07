namespace P06;

public sealed partial class ClientForm {
    Form? clanWindow;
    protected override void OnShown(EventArgs e){
        base.OnShown(e);
        var button=new Button{Text="血盟",Dock=DockStyle.Top,Height=36,AccessibleName="開啟血盟管理"};
        if(normalPanels!=null){var page=new TabPage("血盟");page.Controls.Add(button);normalPanels.TabPages.Add(page);}
        else{button.Dock=DockStyle.Right;button.Width=72;bar.Controls.Add(button);button.BringToFront();}
        button.Click+=(_,_)=>OpenClanWindow();
        ClanInvitation? shown=null;int shownSession=-1;
        var invitations=new System.Windows.Forms.Timer{Interval=250};
        invitations.Tick+=(_,_)=>{var view=login.ClanSnapshot();if(view.Invitation is {} invite&&(shown!=invite||shownSession!=view.Session)){shown=invite;shownSession=view.Session;OpenClanWindow();}};
        FormClosed+=(_,_)=>{invitations.Stop();invitations.Dispose();};invitations.Start();
    }
    void OpenClanWindow(){
        if(clanWindow is {IsDisposed:false}){clanWindow.Activate();return;}
        ClearInput("血盟管理");
        var window=new Form{Text="血盟",ClientSize=new(520,680),StartPosition=FormStartPosition.CenterParent,Font=Font,MinimizeBox=false,MaximizeBox=false};clanWindow=window;
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(12),AutoScroll=true};window.Controls.Add(layout);
        var statusLabel=new Label{Width=475,Height=76};layout.Controls.Add(statusLabel);
        var inviteLabel=new Label{Width=475,Height=30};layout.Controls.Add(inviteLabel);
        var answers=new FlowLayoutPanel{Width=475,Height=35};layout.Controls.Add(answers);
        var accept=new Button{Text="接受入盟",AutoSize=true};var reject=new Button{Text="拒絕入盟",AutoSize=true};answers.Controls.Add(accept);answers.Controls.Add(reject);
        layout.Controls.Add(new Label{Text="建立血盟名稱／驅逐成員名稱",AutoSize=true});
        var name=new TextBox{Width=320,MaxLength=32};layout.Controls.Add(name);
        var actions=new FlowLayoutPanel{Width=475,Height=72};layout.Controls.Add(actions);
        var attention=new TextBox{Width=475,Height=80,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical};
        var announcement=new Label{Width=475,Height=55};
        var members=new ListView{Width=475,Height=170,View=View.Details,FullRowSelect=true};
        ClanMember[] shownMembers=[];
        ClanView view=login.ClanSnapshot();var buttons=new List<Button>();
        foreach(var entry in new[]{(ClanCommand.Create,"建立"),(ClanCommand.Join,"申請加入"),(ClanCommand.Leave,"退出／解散"),(ClanCommand.Ban,"驅逐"),(ClanCommand.Members,"查詢成員")}){
            var button=new Button{Text=entry.Item2,AutoSize=true};buttons.Add(button);actions.Controls.Add(button);
            button.Click+=async(_,_)=>{
                var stamp=view;
                if(entry.Item1 is ClanCommand.Leave or ClanCommand.Ban){string prompt=entry.Item1==ClanCommand.Leave?"確定退出血盟？若你是盟主，伺服器可能解散血盟。":"確定要求驅逐「"+name.Text+"」？";if(MessageBox.Show(window,prompt,"血盟",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;}
                buttons.ForEach(b=>b.Enabled=false);await login.SendClan(entry.Item1,stamp,name.Text);Refresh();
            };
        }
        layout.Controls.Add(attention);
        layout.Controls.Add(announcement);
        members.Columns.Add("成員名稱",210);members.Columns.Add("階級",90);members.Columns.Add("上線狀態",120);layout.Controls.Add(members);
        layout.Controls.Add(new Label{Width=475,Height=45,Text="加入時請與招募者相鄰面對面。職業、權限及條件由伺服器判定。"});
        async Task Answer(bool yes){var stamp=view;accept.Enabled=reject.Enabled=false;await login.AnswerClanInvitation(stamp,yes);Refresh();}
        accept.Click+=async(_,_)=>await Answer(true);reject.Click+=async(_,_)=>await Answer(false);
        void Refresh(){
            view=login.ClanSnapshot();statusLabel.Text=(view.Name.Length>0?"血盟："+view.Name:"尚未收到血盟名稱")+"\n"+view.Notice;
            buttons.ForEach(b=>b.Enabled=view.CanSend&&view.Invitation==null);
            inviteLabel.Text=view.Invitation is {} invite?invite.Name+" 想加入你的血盟。":"目前沒有待處理的入盟申請。";
            accept.Enabled=reject.Enabled=view.CanSend&&view.Invitation!=null;
            attention.Text="關注血盟："+string.Join("、",view.Attention)+(login.World.LastServerMessage is {} message?"\r\n伺服器："+ResolveNpcText(message):"");
            announcement.Text=view.Announcement is {} info?"盟主："+info.Leader+"\n公告："+info.Text:"按「查詢成員」取得伺服器成員列表。";
            if(!shownMembers.SequenceEqual(view.Members)){
                shownMembers=view.Members;members.BeginUpdate();members.Items.Clear();
                foreach(var member in shownMembers)members.Items.Add(new ListViewItem([member.Name,member.Rank.ToString(),member.Online switch{true=>"上線",false=>"離線",_=>"未知"}]));
                members.EndUpdate();
            }
        }
        var refresh=new System.Windows.Forms.Timer{Interval=250};refresh.Tick+=(_,_)=>Refresh();window.FormClosed+=(_,_)=>{refresh.Stop();refresh.Dispose();};Refresh();refresh.Start();window.Show(this);
    }
    void PaintClanLabels(Graphics g){
        if(!login.World.Renderable)return;
        var viewport=new RectangleF(Camera.ViewOffset,Camera.View);
        void Draw(uint id,string name,string clan,uint clanId,PointF foot){
            if(clan.Length==0||name.Length==0)return;
            var p=Camera.ToView(foot);if(!viewport.Contains(p))return;
            string text=name+" ["+clan+"]";var size=g.MeasureString(text,Font);float x=p.X-size.Width/2,y=p.Y-48;
            g.DrawString(text,Font,Brushes.Black,x+1,y+1);g.DrawString(text,Font,Brushes.LightCyan,x,y);
            // S118 broadcasts can be keyed by clan ID; do not reinterpret S72's reserved D.
            if(login.World.FindClanEmblem(clanId) is {} emblem){
                for(int row=0;row<12;row++)for(int col=0;col<16;col++){using var brush=new SolidBrush(Color.FromArgb(emblem.Pixels[row*16+col]));g.FillRectangle(brush,x-19+col,y+row+2,1,1);}
            }
        }
        if(login.World.Actor is {} actor)Draw(actor.ObjectId,actor.Name,actor.Clan,actor.ClanId,renderer.OwnCombatVisual?.Foot??Coordinates.Screen(actor.X,actor.Y));
        foreach(var v in objectVisuals.Where(v=>objectPatch?.VisibleObjectIds.Contains(v.ObjectId)==true)){
            var obj=login.World.Objects.FirstOrDefault(o=>o.ObjectId==v.ObjectId);if(obj!=null)Draw(obj.ObjectId,obj.Name,obj.Clan,obj.ClanId,v.Foot);
        }
    }
}
