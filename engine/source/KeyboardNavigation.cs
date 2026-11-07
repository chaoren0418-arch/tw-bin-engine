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
    bool ProcessLoginKeyboardEnter(ref Message msg,Keys keyData){
        if(ProcessMinimapKeyboard(ref msg,keyData))return true;
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
