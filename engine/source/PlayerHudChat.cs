namespace P06;

public sealed class PlayerHudChatPanel:Panel {
    readonly RichTextBox history=new(){ReadOnly=true,DetectUrls=false,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=PlayerHudPainter.Ink,ForeColor=Color.White,AccessibleName="多頻道聊天歷史，只顯示原服回傳",HideSelection=false};
    readonly TextBox input=new(){Dock=DockStyle.Fill,MaxLength=PlayerChatProtocol.MaxInputCharacters,PlaceholderText="訊息最多 52 字；!喊話 &全體 @血盟 #隊伍 \"對象 密語",AccessibleName="多頻道聊天訊息，正文最多 52 字"};
    readonly Button send=new(){Dock=DockStyle.Right,Width=88,Text="送出",AccessibleName="送出聊天",Enabled=false};
    readonly Label state=new(){Dock=DockStyle.Bottom,Height=38,ForeColor=Color.Silver,AutoEllipsis=true,AccessibleName="聊天傳送狀態"};
    PlayerChatView? displayed;bool pending;
    readonly PlayerChatEnterPress enterPress=new();
    internal void FocusInput(bool byEnter){enterPress.Open(byEnter);if(Visible&&input.CanFocus)input.Focus();}
    internal void ReleaseEntryKey()=>enterPress.Release();
    internal bool DiagnosticInputFocused=>input.Focused;
    public event Func<string,PlayerChatView,Task<bool>>? SubmitRequested;
    public PlayerHudChatPanel(){
        BackColor=PlayerHudPainter.Ink;AccessibleName="多頻道聊天";Padding=new(8);
        var help=new Label{Dock=DockStyle.Bottom,Height=36,ForeColor=Color.Silver,Text=PlayerChatProtocol.InputHelp,AccessibleName=PlayerChatProtocol.InputHelp};
        var row=new Panel{Dock=DockStyle.Bottom,Height=30};row.Controls.AddRange([input,send]);Controls.AddRange([history,state,row]);
        Controls.Add(help);
        input.TextChanged+=(_,_)=>UpdateAvailability();send.Click+=async(_,_)=>await Submit();
        input.PreviewKeyDown+=(_,e)=>{if(e.KeyData==Keys.Enter)e.IsInputKey=true;};
        input.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.Handled=e.SuppressKeyPress=true;if(enterPress.Down(e.Modifiers))await Submit();}};
        input.KeyUp+=(_,e)=>{if(e.KeyCode==Keys.Enter)enterPress.Release();};
    }
    public void UpdateChat(PlayerChatView view){
        bool identity=displayed==null||displayed.Generation!=view.Generation||displayed.Actor!=view.Actor;
        if(identity){input.Clear();history.Clear();}
        if(identity||displayed!.Revision!=view.Revision){
            bool end=history.SelectionStart+history.SelectionLength>=history.TextLength;int caret=history.SelectionStart;
            // Server lines contain original formatting. Never append the submitted draft.
            history.Clear();
            foreach(var line in view.Lines){
                history.SelectionStart=history.TextLength;history.SelectionColor=ChannelColor(line.Channel);
                history.AppendText((history.TextLength>0?Environment.NewLine:"")+PlayerChatProtocol.FormatLine(line));
            }
            history.SelectionStart=end?history.TextLength:Math.Min(caret,history.TextLength);history.SelectionLength=0;if(end)history.ScrollToCaret();
        }
        displayed=view;UpdateAvailability();
    }
    public static Color ChannelColor(PlayerChatChannel channel)=>channel switch{PlayerChatChannel.Shout=>Color.Orange,PlayerChatChannel.Global=>Color.LightSkyBlue,PlayerChatChannel.Whisper=>Color.Violet,PlayerChatChannel.Clan=>Color.LightGreen,PlayerChatChannel.Party=>Color.Yellow,_=>Color.White};
    void UpdateAvailability(){
        bool valid=false;string detail="正文 0/52 字";
        try{
            var message=PlayerChatProtocol.ParseInput(input.Text);
            detail=$"{PlayerChatProtocol.ChannelName(message.Channel)} · 正文 {message.Text.Length}/52 字";
            PlayerChatProtocol.Encode(message);valid=true;
        }catch(ArgumentException ex){if(input.TextLength>0)detail=ex.Message;}
        send.Enabled=!pending&&displayed?.CanSend==true&&valid;
        state.Text=$"{detail} · {(displayed?.Notice.Length>0?displayed.Notice:"訊息以原服回傳為準")}";
        state.AccessibleName=state.Text;input.AccessibleDescription=detail+"；Enter 送出。"+PlayerChatProtocol.InputHelp;
    }
    async Task Submit(){
        if(!send.Enabled||pending||displayed is not {} stamp||SubmitRequested==null)return;
        string text=input.Text;pending=true;UpdateAvailability();
        try{
            bool sent=await SubmitRequested(text,stamp);
            if(!IsDisposed&&sent&&displayed?.Generation==stamp.Generation&&displayed.Actor==stamp.Actor&&input.Text==text)input.Clear();
        }finally{pending=false;if(!IsDisposed)UpdateAvailability();}
    }
}
public sealed partial class ClientForm {
    bool PlayerChatFocused=>playerHud?.ChatContainsFocus==true;
    void InitPlayerHudChat(){
        if(playerHud==null)return;
        playerHud.ChatOpenRequested+=()=>OpenPlayerChat(false);
        playerHud.ChatEntered+=()=>LeaveGameFocus("player-chat");
        playerHud.ChatSubmitRequested+=async(text,displayed)=>{
            var current=login.PlayerChatSnapshot();
            if(current.Generation!=displayed.Generation||current.WorldEpoch!=displayed.WorldEpoch||current.Actor!=displayed.Actor)return false;
            ClearInput("player-chat-send");
            bool sent=await login.SendPlayerChat(text,current.Generation,current.WorldEpoch,current.Actor);
            if(!IsDisposed)playerHud.UpdateChat(login.PlayerChatSnapshot());return sent;
        };
    }
}
