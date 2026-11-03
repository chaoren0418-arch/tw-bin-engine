namespace P06;

// Safe fixed labels and focus flags only. Never include control content or window captions.
public sealed record NativeFocusSnapshot(bool formFocused,bool containsFocus,bool activeFormIsThis,bool gameFocus,string focusedControl,string focusedControlType,string hudPanel);

public sealed partial class ClientForm {
    static Control? FindNativeFocusedControl(Control root){
        if(root.Focused)return root;
        foreach(Control child in root.Controls)
            if(child.ContainsFocus&&FindNativeFocusedControl(child) is {} focused)return focused;
        return null;
    }
    NativeFocusSnapshot CaptureNativeFocusSnapshot(){
        bool formFocused=Focused,containsFocus=ContainsFocus;
        var focused=containsFocus?FindNativeFocusedControl(this):null;
        string identity=focused==loginAccount?"account":focused==loginPassword?"password":focused==charactersBox?"character":
            focused==loginConnect?"login":focused==enterWorld?"enterWorld":
            playerHud?.DiagnosticChatInputFocused==true?"chat-input":
            playerHud?.ContainsFocus==true?"HUD":web?.ContainsFocus==true?"webview":
            formFocused?"form":focused!=null?"other":containsFocus?"native-child":"none";
        // Class labels are an allowlist, not control Name/Text/AccessibleName/GetType strings.
        string kind=focused switch {
            null=>"none",Form=>"form",RichTextBox=>"rich-text-box",TextBoxBase=>"text-box",
            ButtonBase=>"button",ComboBox=>"combo-box",NumericUpDown=>"numeric-input",
            ListControl=>"list",TabControl=>"tabs",Panel=>"panel",_=>"control"
        };
        string panel=normalPanels?.Visible==true?"services":playerHud?.DiagnosticPanel??"absent";
        return new(formFocused,containsFocus,ActiveForm==this,gameFocus,identity,kind,panel);
    }
}
