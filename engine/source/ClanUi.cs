namespace P06;

public sealed partial class ClientForm {
    Form? clanWindow;bool clanUiHooked;
    // OnLoad/OnShown belong to other partial modules; the Shown event keeps the same timing.
    protected override void OnCreateControl(){
        if(!clanUiHooked){clanUiHooked=true;Shown+=(_,_)=>InitClanUi();}
        base.OnCreateControl();
    }
    void InitClanUi(){
        var button=new Button{Text="血盟",Dock=DockStyle.Top,Height=36,AccessibleName="開啟血盟管理"};
        if(normalPanels!=null){var page=new TabPage("血盟");page.Controls.Add(button);normalPanels.TabPages.Add(page);}
        else{button.Dock=DockStyle.Right;button.Width=72;bar.Controls.Add(button);button.BringToFront();}
        button.Click+=(_,_)=>OpenClanWindow();
        // R2-K (#625): the join question (S219/97) is answered on the chat input line (shared slot, YesNoPrompt.cs), so it
        // no longer opens this window. The poll stays: it re-checks the invitation so a stale one leaves the slot promptly.
        var invitations=new System.Windows.Forms.Timer{Interval=250};
        invitations.Tick+=(_,_)=>{if(login.State==LoginState.InWorld)login.ClanSnapshot();};
        FormClosed+=(_,_)=>{invitations.Stop();invitations.Dispose();};invitations.Start();
    }
    void OpenClanWindow(){
        if(clanWindow is {IsDisposed:false}){clanWindow.Activate();return;}
        ClearInput("血盟管理");
        var window=new Form{Text="血盟",ClientSize=new(520,680),StartPosition=FormStartPosition.CenterParent,Font=Font,MinimizeBox=false,MaximizeBox=false};clanWindow=window;
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(12),AutoScroll=true};window.Controls.Add(layout);
        var statusLabel=new Label{Width=475,Height=76};layout.Controls.Add(statusLabel);
        var inviteLabel=new Label{Width=475,Height=30};layout.Controls.Add(inviteLabel);
        layout.Controls.Add(new Label{Text="建立血盟名稱／驅逐成員名稱／設定階級的成員名稱",AutoSize=true});
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
        // R2-K: C63 rank change (2–10, names from the server's C_Rank comment) and C18 emblem upload. Entry points are
        // engine-defined (the original uses PledgeUI's ChangeRank popup, WIN2 §3, and an upload site not traced yet).
        var leaderActions=new FlowLayoutPanel{Width=475,Height=36};layout.Controls.Add(leaderActions);
        var rankBox=new ComboBox{Width=150,DropDownStyle=ComboBoxStyle.DropDownList};
        for(byte rank=ClanProtocol.MinRank;rank<=ClanProtocol.MaxRank;rank++)rankBox.Items.Add($"{rank} {ClanProtocol.RankName(rank)}");
        rankBox.SelectedIndex=0;
        var rankButton=new Button{Text="設定階級",AutoSize=true};var emblemButton=new Button{Text="上傳盟徽…",AutoSize=true};
        leaderActions.Controls.AddRange([rankBox,rankButton,emblemButton]);buttons.Add(rankButton);buttons.Add(emblemButton);
        rankButton.Click+=async(_,_)=>{
            var stamp=view;string target=name.Text.Trim();byte rank=(byte)(ClanProtocol.MinRank+rankBox.SelectedIndex);
            if(MessageBox.Show(window,$"確定把「{target}」的階級設為 {rank}（{ClanProtocol.RankName(rank)}）？","血盟",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            buttons.ForEach(b=>b.Enabled=false);await login.SendClanRank(stamp,target,rank);Refresh();
        };
        emblemButton.Click+=async(_,_)=>{
            var stamp=view;
            using var dialog=new OpenFileDialog{Title="選擇 16×12 盟徽 BMP",Filter="BMP 圖檔 (*.bmp)|*.bmp",CheckFileExists=true};
            if(dialog.ShowDialog(window)!=DialogResult.OK)return;
            int[] pixels;
            try{
                var file=new FileInfo(dialog.FileName);if(file.Length>65536)throw new ArgumentException("盟徽檔太大。");
                pixels=ClanEmblemProtocol.ReadUploadFile(File.ReadAllBytes(dialog.FileName));
            }catch(Exception ex) when(ex is ArgumentException or IOException or UnauthorizedAccessException){MessageBox.Show(window,ex.Message,"血盟",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            if(MessageBox.Show(window,"確定上傳這個盟徽？只有血盟君主能上傳，結果以伺服器回報為準。","血盟",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            buttons.ForEach(b=>b.Enabled=false);await login.UploadClanEmblem(stamp,pixels);Refresh();
        };
        layout.Controls.Add(attention);
        layout.Controls.Add(announcement);
        members.Columns.Add("成員名稱",210);members.Columns.Add("階級",90);members.Columns.Add("上線狀態",120);layout.Controls.Add(members);
        members.SelectedIndexChanged+=(_,_)=>{if(members.SelectedItems.Count==1)name.Text=members.SelectedItems[0].Text;};
        layout.Controls.Add(new Label{Width=475,Height=45,Text="加入時請與招募者相鄰面對面。入盟申請請在聊天輸入列輸入 Y／N 回答。職業、權限及條件由伺服器判定。"});
        void Refresh(){
            view=login.ClanSnapshot();
            string show=login.World.ClanShowEmblem switch{true=>"（顯示盟徽：開）",false=>"（顯示盟徽：關）",_=>""};
            statusLabel.Text=(view.Name.Length>0?"血盟："+view.Name+show:"尚未收到血盟名稱")+"\n"+view.Notice;
            buttons.ForEach(b=>b.Enabled=view.CanSend&&view.Invitation==null);
            inviteLabel.Text=view.Invitation is {} invite?invite.Name+" 想加入你的血盟；請在聊天輸入列輸入 Y 接受、N 拒絕。":"目前沒有待處理的入盟申請。";
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
    // The world view never draws the clan name (+244); names and emblems are painted by ObjectRendering.
}
