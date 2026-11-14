using System.Runtime.InteropServices;
using P06.NativeUi;
namespace P06;

// f1247-ime-path (#1271): IME composition for the native text inputs. Pure rules: NativeUi/UiIme.cs; report: docs/reports/issue-1271.md.
// Only while the native UI is active (NativeUiActive); --classic-ui / nativeUi:false never reaches anything below (WndProc and
// PreProcessMessage fall straight through to the base, so the WinForms TextBox path is unchanged).
// - The focused ClientForm keeps its WinForms default (Form.DefaultImeMode = NoControl), so WinForms does not detach its input
//   context; the engine still checks it when a text input starts (no context → IACE_DEFAULT) and logs what it found.
// - VK_PROCESSKEY is left to TranslateMessage while a text input takes IME text (otherwise ProcessCmdKey → native panels could
//   swallow it and no WM_IME_* would ever be generated).
// - Composition window (CFS_POINT) at the caret, candidate list (CFS_EXCLUDE) clear of the input line, like TW16 sub_A37E30
//   (ImmSetCompositionWindow CFS_POINT at the edit caret) and sub_A35950 (IMN_OPENCANDIDATE → ImmSetCandidateWindow). The
//   system IME UI draws both; the engine never draws the composition string.
// - WM_IME_CHAR goes once to the native text input (UiImeCharGate) and is not passed on, so no WM_CHAR follows.
// - IME log lines (WebLog → web-events.jsonl): ime-focus, ime-start, ime-comp, ime-result, ime-end, ime-char, ime-status,
//   ime-action. Lengths and counts only, never the text. Every line is written from a message actually received.
public sealed partial class ClientForm {
    readonly UiImeCharGate nativeImeGate=new();
    readonly UiImeSession nativeImeSession=new();
    bool nativeImeComposing;string? nativeImeTarget;

    void InitNativeIme(){
        timer.Tick+=(_,_)=>SyncNativeIme("tick");
        events.Add(new{kind="native-ui-ime",state="attached"});
    }

    (IUiPanel Panel,Rectangle Caret)? NativeImeTarget(){
        if(!NativeUiActive||nativeUiRoot is not {} root)return null;
        return UiImeTargets.Find(root.HitOrder(),p=>root.Usable(p)&&!root.Faulted.Contains(p));
    }

    /// <summary>Starts / ends the IME text session when a native text input starts / stops taking text.</summary>
    void SyncNativeIme(string why){
        if(!IsHandleCreated||IsDisposed)return;
        var target=NativeImeTarget();
        if(target is {} t){
            if(nativeImeSession.Active){nativeImeTarget=t.Panel.Id;return;}
            // The IME talks to the focused window; a native text input is typed into the form itself (KeyPreview / KeyPress).
            if(ContainsFocus&&!Focused){
                ImeLog("ime-action",new{action="focus-form",why,focus=FocusLabel()});
                ActiveControl=null;Focus();
            }
            var s=ReadIme();
            nativeImeTarget=t.Panel.Id;
            foreach(var a in nativeImeSession.Enter(s.Context,s.Open,s.Conversion))ApplyIme(a,why);
            ImeFocusLog("enter",why);
            PlaceNativeIme(t.Caret);
        }else if(nativeImeSession.Active){
            var s=ReadIme();
            foreach(var a in nativeImeSession.Leave(nativeImeComposing,s.Open,s.Conversion))ApplyIme(a,why);
            nativeImeComposing=false;nativeImeGate.Reset();
            ImeFocusLog("leave",why);
            nativeImeTarget=null;
        }
    }

    void ApplyIme(UiImeAction a,string why){
        nint hwnd=Handle;bool ok;
        switch(a){
        case UiImeAction.Associate:ok=ImmAssociateContextEx(hwnd,0,IACE_DEFAULT);break;
        case UiImeAction.RestoreDetached:ImmAssociateContext(hwnd,0);ok=true;break;
        default:{
            nint himc=ImmGetContext(hwnd);
            if(himc==0){ok=false;break;}
            try{
                ok=a switch{
                    UiImeAction.Close=>ImmSetOpenStatus(himc,false),
                    UiImeAction.CancelComposition=>ImmNotifyIME(himc,NI_COMPOSITIONSTR,CPS_CANCEL,0),
                    UiImeAction.RestoreConversion=>ImmGetConversionStatus(himc,out _,out int sentence)&&ImmSetConversionStatus(himc,nativeImeSession.ConversionBefore,sentence),
                    _=>false
                };
            }finally{ImmReleaseContext(hwnd,himc);}
            break;
        }
        }
        ImeLog("ime-action",new{action=a.ToString(),why,ok,conversion=a==UiImeAction.RestoreConversion?nativeImeSession.ConversionBefore:(int?)null});
    }

    /// <summary>Composition point and candidate exclusion at the caret of the current target (client pixels, see UiImePlacement).</summary>
    UiImePlacement? PlaceNativeIme(Rectangle? caret=null){
        caret??=NativeImeTarget()?.Caret;
        if(caret is not {} c||!IsHandleCreated)return null;
        var p=UiImePlacement.Compute(c,NativeUiViewport(),ClientSize);
        nint hwnd=Handle,himc=ImmGetContext(hwnd);
        if(himc==0)return p;
        try{
            var cf=new COMPOSITIONFORM{dwStyle=CFS_POINT,ptCurrentPos=new(p.Composition.X,p.Composition.Y),rcArea=default};
            ImmSetCompositionWindow(himc,ref cf);
            var cand=new CANDIDATEFORM{dwIndex=0,dwStyle=CFS_EXCLUDE,ptCurrentPos=new(p.Exclude.X,p.Exclude.Bottom),
                rcArea=new(p.Exclude.Left,p.Exclude.Top,p.Exclude.Right,p.Exclude.Bottom)};
            ImmSetCandidateWindow(himc,ref cand);
        }finally{ImmReleaseContext(hwnd,himc);}
        return p;
    }

    public override bool PreProcessMessage(ref Message msg){
        if(NativeUiActive&&UiImeKeys.PassToIme(msg.Msg,msg.WParam)){
            if(!nativeImeSession.Active)SyncNativeIme("process-key");
            // While a native text input takes IME text, VK_PROCESSKEY is the IME's: TranslateMessage must see it.
            if(nativeImeSession.Active)return false;
        }
        return base.PreProcessMessage(ref msg);
    }

    protected override void WndProc(ref Message m){
        if(!NativeUiActive){base.WndProc(ref m);return;}
        switch(m.Msg){
        case UiImeKeys.WM_KEYDOWN:case UiImeKeys.WM_SYSKEYDOWN:
            nativeImeGate.KeyDown((int)m.WParam&0xFFFF);break;
        case WM_CHAR:
            if(nativeImeGate.IsEcho((char)(int)m.WParam)){ImeLog("ime-char",new{source="WM_CHAR",echoDropped=true});m.Result=0;return;}
            break;
        case WM_SETFOCUS:case WM_KILLFOCUS:
            base.WndProc(ref m);
            ImeFocusLog(m.Msg==WM_SETFOCUS?"set-focus":"kill-focus",null);
            return;
        case WM_IME_STARTCOMPOSITION:{
            SyncNativeIme("start-composition");
            nativeImeComposing=true;
            var p=PlaceNativeIme();
            ImeLog("ime-start",new{target=nativeImeTarget,dpi=DeviceDpi,client=new{ClientSize.Width,ClientSize.Height},
                composition=p is {} q?new{q.Composition.X,q.Composition.Y}:null,exclude=p is {} e?new{e.Exclude.X,e.Exclude.Y,e.Exclude.Width,e.Exclude.Height}:null});
            break;
        }
        case WM_IME_COMPOSITION:{
            long flags=(long)m.LParam;
            int comp=-1,result=-1;
            nint hwnd=Handle,himc=ImmGetContext(hwnd);
            if(himc!=0){
                try{
                    if((flags&GCS_COMPSTR)!=0)comp=ImmGetCompositionStringW(himc,GCS_COMPSTR,0,0)/2;
                    if((flags&GCS_RESULTSTR)!=0)result=ImmGetCompositionStringW(himc,GCS_RESULTSTR,0,0)/2;
                }finally{ImmReleaseContext(hwnd,himc);}
            }
            ImeLog("ime-comp",new{target=nativeImeTarget,flags=$"0x{flags:X}",compLength=comp,context=himc!=0});
            if(result>=0)ImeLog("ime-result",new{target=nativeImeTarget,chars=result});
            PlaceNativeIme();
            break;
        }
        case WM_IME_ENDCOMPOSITION:
            nativeImeComposing=false;
            ImeLog("ime-end",new{target=nativeImeTarget,delivered=nativeImeGate.Delivered,echoDropped=nativeImeGate.Dropped});
            break;
        case WM_IME_NOTIFY:{
            int code=(int)m.WParam;
            if(code is IMN_OPENCANDIDATE or IMN_CHANGECANDIDATE)PlaceNativeIme();
            if(code is IMN_SETOPENSTATUS or IMN_SETCONVERSIONMODE or IMN_OPENCANDIDATE or IMN_CLOSECANDIDATE){
                base.WndProc(ref m);
                var s=ReadIme();
                ImeLog("ime-status",new{notify=code switch{IMN_SETOPENSTATUS=>"open-status",IMN_SETCONVERSIONMODE=>"conversion",IMN_OPENCANDIDATE=>"open-candidate",_=>"close-candidate"},
                    context=s.Context,open=s.Open,conversion=s.Conversion});
                return;
            }
            break;
        }
        case WM_IME_CHAR:{
            // One delivery: straight to the native text input, never on to DefWindowProc (which would post a WM_CHAR).
            bool used=nativeImeGate.ImeChar((char)(int)m.WParam,NativeUiChar);
            ImeLog("ime-char",new{source="WM_IME_CHAR",target=nativeImeTarget,used});
            m.Result=0;return;
        }
        }
        base.WndProc(ref m);
    }

    // ---- state and log ----
    readonly record struct ImeState(nint Focus,bool FocusIsForm,bool Context,bool Open,int Conversion,nint Layout);
    ImeState ReadIme(){
        nint hwnd=Handle,focus=GetFocus(),himc=ImmGetContext(hwnd);
        bool open=false;int conversion=0;
        if(himc!=0){
            try{open=ImmGetOpenStatus(himc);if(ImmGetConversionStatus(himc,out int c,out _))conversion=c;}
            finally{ImmReleaseContext(hwnd,himc);}
        }
        return new(focus,focus==hwnd,himc!=0,open,conversion,GetKeyboardLayout(0));
    }
    string FocusLabel(){try{return CaptureNativeFocusSnapshot().focusedControlType;}catch(Exception ex) when(ex is InvalidOperationException or ObjectDisposedException){return "unknown";}}
    void ImeFocusLog(string phase,string? why){
        if(!IsHandleCreated)return;
        var s=ReadIme();
        ImeLog("ime-focus",new{phase,why,form=$"0x{(long)Handle:X}",focus=$"0x{(long)s.Focus:X}",s.FocusIsForm,focusControl=FocusLabel(),
            context=s.Context,open=s.Open,conversion=$"0x{s.Conversion:X}",layout=$"0x{(long)s.Layout:X}",session=nativeImeSession.Active,target=nativeImeTarget,
            imeWindow=$"0x{(long)ImmGetDefaultIMEWnd(Handle):X}"});
    }
    void ImeLog(string kind,object data){try{WebLog(kind,data);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ObjectDisposedException){}}

    // ---- Win32 ----
    const int WM_SETFOCUS=0x0007,WM_KILLFOCUS=0x0008,WM_CHAR=0x0102,WM_IME_STARTCOMPOSITION=0x010D,WM_IME_ENDCOMPOSITION=0x010E,
        WM_IME_COMPOSITION=0x010F,WM_IME_NOTIFY=0x0282,WM_IME_CHAR=0x0286;
    const int IMN_CHANGECANDIDATE=0x0003,IMN_CLOSECANDIDATE=0x0004,IMN_OPENCANDIDATE=0x0005,IMN_SETCONVERSIONMODE=0x0006,IMN_SETOPENSTATUS=0x0008;
    const int GCS_COMPSTR=0x0008,GCS_RESULTSTR=0x0800,CFS_POINT=0x0002,CFS_EXCLUDE=0x0080,IACE_DEFAULT=0x0010,NI_COMPOSITIONSTR=0x0015,CPS_CANCEL=0x0004;
    [StructLayout(LayoutKind.Sequential)] struct ImePoint(int x,int y){public int X=x,Y=y;}
    [StructLayout(LayoutKind.Sequential)] struct ImeRect(int l,int t,int r,int b){public int Left=l,Top=t,Right=r,Bottom=b;}
    [StructLayout(LayoutKind.Sequential)] struct COMPOSITIONFORM{public int dwStyle;public ImePoint ptCurrentPos;public ImeRect rcArea;}
    [StructLayout(LayoutKind.Sequential)] struct CANDIDATEFORM{public int dwIndex;public int dwStyle;public ImePoint ptCurrentPos;public ImeRect rcArea;}
    [DllImport("imm32.dll")] static extern nint ImmGetContext(nint hwnd);
    [DllImport("imm32.dll")] static extern bool ImmReleaseContext(nint hwnd,nint himc);
    [DllImport("imm32.dll")] static extern bool ImmGetOpenStatus(nint himc);
    [DllImport("imm32.dll")] static extern bool ImmSetOpenStatus(nint himc,bool open);
    [DllImport("imm32.dll")] static extern bool ImmGetConversionStatus(nint himc,out int conversion,out int sentence);
    [DllImport("imm32.dll")] static extern bool ImmSetConversionStatus(nint himc,int conversion,int sentence);
    [DllImport("imm32.dll")] static extern bool ImmSetCompositionWindow(nint himc,ref COMPOSITIONFORM form);
    [DllImport("imm32.dll")] static extern bool ImmSetCandidateWindow(nint himc,ref CANDIDATEFORM form);
    [DllImport("imm32.dll",CharSet=CharSet.Unicode)] static extern int ImmGetCompositionStringW(nint himc,int index,nint buffer,int length);
    [DllImport("imm32.dll")] static extern nint ImmAssociateContext(nint hwnd,nint himc);
    [DllImport("imm32.dll")] static extern bool ImmAssociateContextEx(nint hwnd,nint himc,int flags);
    [DllImport("imm32.dll")] static extern bool ImmNotifyIME(nint himc,int action,int index,int value);
    [DllImport("imm32.dll")] static extern nint ImmGetDefaultIMEWnd(nint hwnd);
    [DllImport("user32.dll")] static extern nint GetFocus();
    [DllImport("user32.dll")] static extern nint GetKeyboardLayout(int thread);
}
