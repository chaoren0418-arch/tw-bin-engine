namespace P06;

public sealed partial class ClientForm {
    // Actual Form focus is required, in addition to the existing game/chat/WebView gate.
    // A stale gameFocus flag must never turn a native button/textbox key into a world action.
    internal static bool OwnsGameKeyboardKey(Keys keyData,bool inputAllowed,bool formFocused)=>
        inputAllowed&&formFocused&&(keyData&Keys.Modifiers)==Keys.None&&GameKey(keyData&Keys.KeyCode);
    bool OwnsGameKeyboardKey(Keys keyData)=>OwnsGameKeyboardKey(keyData,GameInputAllowed,Focused);
    protected override bool IsInputKey(Keys keyData){
        if((keyData&Keys.KeyCode) is Keys.Tab or Keys.Enter)return false;
        return OwnsGameKeyboardKey(keyData)||base.IsInputKey(keyData);
    }
    // 0: leave to the focused control; 1: consume without submission; 2: guarded button click.
    internal static int LoginEnterDisposition(Keys keyData,bool normal,bool credentialFocused,bool canLogin,bool buttonEnabled,bool repeat)=>
        keyData!=Keys.Enter||!normal||!credentialFocused?0:canLogin&&buttonEnabled&&!repeat?2:1;
    // R2-J (#624, WO §6): Esc (27) on the input line takes sub_A58380's path like Enter with a non-y text, so a shown
    // server question is answered "no" (C121 C0). Only a fresh press while this form (game view or chat line) has focus;
    // otherwise Esc keeps its existing meaning (close chat, clear input). The native canvas handles it in YesNoPanel first.
    internal static bool YesNoEscapeDeclines(Keys keyData,bool inWorld,bool prompt,bool formFocus,bool repeat)=>
        keyData==Keys.Escape&&inWorld&&prompt&&formFocus&&!repeat;
    bool ProcessYesNoEscape(ref Message msg,Keys keyData){
        if(keyData!=Keys.Escape||ActiveForm!=this)return false;
        var view=login.YesNoSnapshot();
        if(!YesNoEscapeDeclines(keyData,login.State==LoginState.InWorld,view.Prompt!=null,Focused||PlayerChatFocused,((long)msg.LParam&(1L<<30))!=0))return false;
        _=AnswerYesNoShown(view,false,"yes-no-escape");
        return true;
    }
    // T403 (#918, gap-matrix 15.12.6 小清理): Esc while an item-delete prompt waits on the chat line (R6-E / R9-L, sub_AC98E0)
    // cancels it (CancelItemDelete, nothing sent) — like the native ChatPanel's R10-F Esc. Here for the WinForms chat line
    // and inventory (list, trash button, game view), and for the native inventory window when no native panel took the Esc
    // (ItemWindowPanel itself does not take it, so the native ChatPanel normally cancels first). After the server's YES/NO
    // (that prompt is older on the same line; the original order is Q8-3, not traced). The chat line closes as with its own Esc.
    internal static bool ItemDeleteEscapeCancels(Keys keyData,bool inWorld,bool prompt,bool ownerFocus,bool repeat)=>
        keyData==Keys.Escape&&inWorld&&prompt&&ownerFocus&&!repeat;
    bool ProcessItemDeleteEscape(ref Message msg,Keys keyData){
        if(keyData!=Keys.Escape||ActiveForm!=this)return false;
        bool owner=Focused||PlayerChatFocused||playerHud?.ChatButtonFocused==true||inventoryList.Focused||deleteItemButton.Focused;
        if(!ItemDeleteEscapeCancels(keyData,login.State==LoginState.InWorld,login.ItemDeletePromptCurrent!=null,owner,((long)msg.LParam&(1L<<30))!=0))return false;
        if(!login.CancelItemDelete())return false;
        if(playerHud!=null){playerHud.UpdateChat(login.PlayerChatSnapshot());if(playerHud.ChatPanelOpen){ExitPlayerChat();return true;}}
        PublishWebState();
        return true;
    }
    bool ProcessLoginKeyboardEnter(ref Message msg,Keys keyData){
        if(ProcessYesNoEscape(ref msg,keyData))return true; // Same ProcessCmdKey slot, before chat's own Esc (PlayerChatKeyboardFlow).
        if(ProcessItemDeleteEscape(ref msg,keyData))return true;
        int disposition=LoginEnterDisposition(keyData,normalProduct,loginAccount.Focused||loginPassword.Focused,loginControl?.CanLogin==true,loginConnect.Enabled,((long)msg.LParam&(1L<<30))!=0);
        if(disposition==2)loginConnect.PerformClick(); // Existing Click -> StartLogin -> guarded LoginControl.Start.
        return disposition!=0;
    }
    void InitLoginKeyboardNavigation(){
        if(!normalProduct)return;
        Control[] order=[loginAccount,loginPassword,loginConnect,charactersBox,enterWorld,loginCancel,loginOffline,loginProfile,loginPort];
        for(int i=0;i<order.Length;i++)order[i].TabIndex=i;
        loginLabel.TabStop=false;
    }
}
