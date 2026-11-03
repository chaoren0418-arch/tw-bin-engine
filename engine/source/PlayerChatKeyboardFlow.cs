namespace P06;

// An opening Enter remains held after focus moves to the editor. It must be released
// before the editor can treat a later Enter as an explicit submit, even with a saved draft.
public sealed class PlayerChatEnterPress {
    bool held;
    public void Open(bool byEnter)=>held|=byEnter;
    public bool Down(Keys modifiers){bool fresh=!held;held=true;return fresh&&modifiers==Keys.None;}
    public void Release()=>held=false;
}
public static class PlayerChatFlowRules {
    public static bool CanOpen(bool normal,LoginState state,bool activeForm,bool hudVisible,bool canSend)=>
        normal&&state==LoginState.InWorld&&activeForm&&hudVisible&&canSend;
}
public sealed partial class ClientForm {
    void OpenPlayerChat(bool byEnter){
        var current=login.PlayerChatSnapshot();
        if(!PlayerChatFlowRules.CanOpen(normalProduct,login.State,ActiveForm==this,playerHud?.Visible==true,current.CanSend))return;
        ClearInput("chat-open");playerHudHeld.Clear();LeaveGameFocus("chat-open");
        playerHud!.UpdateChat(current);playerHud.OpenChatInput(byEnter);PublishWebState();
    }
    void ExitPlayerChat(){
        playerHud?.CloseChatInput();playerHudHeld.Clear();ReturnGameFocus();PublishWebState();
    }
    bool ProcessPlayerChatKeyboard(ref Message msg,Keys keyData){
        if(!normalProduct||playerHud==null||!playerHud.Visible||ActiveForm!=this)return false;
        if(keyData==Keys.Escape&&playerHud.ChatPanelOpen&&(PlayerChatFocused||playerHud.ChatButtonFocused||Focused)){
            ExitPlayerChat();return true;
        }
        if(keyData==Keys.Enter&&login.State==LoginState.InWorld&&
            (GameInputAllowed&&Focused||playerHud.ChatButtonFocused)){
            if(((long)msg.LParam&(1L<<30))==0)OpenPlayerChat(true);
            return true; // Consume opening/repeated Enter; never route that message to Submit.
        }
        return false;
    }
}
