using System.Globalization;

// T326 (#764, P-D of docs/reference/pak-resources.md §6, row 4:LoginUI): the native login screen, windows "Login_Window" and
// "Message_Window" of LoginUI.xml. LoginUI.xml is not in 0-客戶端 (PAK§4: all paks checked) and none of its 44 surfs are
// (docs/reports/issue-764.md lists the pak / file search); both are read at run time from the reference client: the XML
// through UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak — kor25 by default, PAK§3;
// SHA-256 logged as "ui-xml-ref"), the surfs through SurfCatalog.Get (local, then the T322/#760 reference Sprite*.pak,
// SHA-256 logged as "surf-ref"). Nothing is copied into the repository. Missing images are not drawn (no substitute) and are
// listed in Missing.
//
// Wiring. UiRoot.Build is all-or-nothing, so the registration uses the host layout "uistatus86" (always in the local Tile.pak,
// same as OptionPanel / AgitAuctionPanel) and Load reads LoginUI.xml itself: no reference client / no XML / malformed XML →
// Unavailable, the panel never shows and the rest of the native UI keeps working. The login itself goes through
// LoginControl.NativeEntry.StartNative (LoginControl.cs native branch): the same guarded LoginControl.Start as the WinForms
// strip and the web page, with the normal-entry endpoint (ProductEntry). With --classic-ui (or when the panel is
// unavailable) nothing changes: the existing WinForms / web login stays the only entry.
//
// What is built:
//   Login_Window (X/Y 0,0; no size, no BaseSurf): every Image with a Surf, the Buttons (Base/Highlight/Push surf), and the two
//   InputBoxes (ID_Input, Password_Input: X/Y/Width/Height, LimitText, Password="1" drawn as '*'). The window has no declared
//   size, so Bounds = the union of the built pieces (the world outside stays reachable, e.g. the offline preview).
//   Back_Image has no Surf (the background is drawn at run time by code outside the XML) → skipped, nothing is invented.
//   Login_Button (Login_LoginWin) and Enter in the password box log in; Enter in the ID box moves to the password box (推測);
//   Tab switches boxes; Esc leaves the boxes (keys go to the game again). The ID box takes focus when the screen shows (推測).
//   Message_Window (Modal): opened when the native login is refused (LoginControl message) or the session ends in
//   Rejected / Disconnected / Faulted after a native login (ProductEntry.StateName). Size = BaseSurf 7092 (no declared size;
//   BaseSurf missing → no native message window, logged once). Message_Label (MultiLabel) wraps the text inside its
//   Width/Height, VAlign center; Ok_Button / Cancel_Button, Enter / Esc close it. While it shows it takes all input (Modal).
// Paint order (推測, same rule as AgitAuctionPanel #761): children in reverse XML order (Back_Image is listed last in both
// windows yet must be at the bottom); hit-testing is the reverse of paint order. The ";n" flag after a surf number is kept
// (Flag) but not interpreted (待驗證). FontSize, sounds (OverSound / PushSound / FocusSound) and DarkBack are not applied.
// Not built / not wired (待做, see docs/reports/issue-764.md): Notice_Window (S48 news stays the existing message box),
// Credit_Window (TextScroller), ChangePass_Button / OTP_Button / Credit_Button / Exit_Button — clicks are logged once as
// "login-ui … 尚未接上". The character select screen (SelectUI.xml) is a separate task; after the character list arrives the
// login screen hides and the existing select UI takes over.

namespace P06.NativeUi {
    /// <summary>"BaseSurf=\"6995;1\"" → Attr BaseSurf, Id 6995, Flag 1 (Flag 0 when absent; meaning 待驗證).</summary>
    public sealed record LoginImageRef(string Attr,int Id,int Flag) {
        public override string ToString()=>$"surf {Id}";
    }

    public enum LoginPieceKind { Image, Button, Input, Label }

    /// <summary>
    /// A drawable / clickable element of one window. Offset is relative to the window origin. Surface null = missing image
    /// (not drawn). Inputs and labels carry their declared Width/Height.
    /// </summary>
    public sealed record LoginPiece(LoginPieceKind Kind,string Path,string? Name,Point Offset,Size DeclaredSize,
        LoginImageRef? Base,UiSurface? BaseSurface,UiSurface? HighlightSurface,UiSurface? PushSurface,string? MouseEvent,
        bool Password=false,int Limit=0) {
        public Size ImageSize=>BaseSurface==null?Size.Empty:new(BaseSurface.Width,BaseSurface.Height);
        public Rectangle Local=>new(Offset,Kind is LoginPieceKind.Input or LoginPieceKind.Label||DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
        public bool Clickable=>Kind is LoginPieceKind.Button or LoginPieceKind.Input&&Local.Width>0&&Local.Height>0;
    }

    /// <summary>One built window: origin (XML X/Y), size (Message_Window: its BaseSurf), base image, pieces in paint order.</summary>
    public sealed record LoginWindow(string Name,Point Origin,Size Size,LoginPiece? BaseImage,IReadOnlyList<LoginPiece> Pieces) {
        public LoginPiece? Find(string name)=>Pieces.FirstOrDefault(p=>p.Name==name);
        /// <summary>Union of the pieces that have an area (window-relative); used when the window declares no size.</summary>
        public Rectangle Extent{get{
            var r=Rectangle.Empty;
            foreach(var p in Pieces){var l=p.Local;if(l.Width<=0||l.Height<=0)continue;r=r.IsEmpty?l:Rectangle.Union(r,l);}
            return r;
        }}
    }

    public sealed class LoginPanel(UiResources? resources=null,Func<LoginControl?>? source=null):IUiClosablePanel,IUiTextInput {
        [UiPanel(190)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new LoginPanel(root.Resources));   // T326 (#764): LoginUI.xml from the reference client (P-A), loaded inside Load
        public const string PanelId="LoginUI";
        public const string LayoutName="LoginUI",WindowNode="Window",LoginWindowName="Login_Window",MessageWindowName="Message_Window";
        public const string IdInput="ID_Input",PasswordInput="Password_Input",MessageLabel="Message_Label";
        public const string LoginEvent="Login_LoginWin",MessageOkEvent="Ok_MessageWin",MessageCancelEvent="Cancel_MessageWin";
        /// <summary>Layout handed to UiRoot.Build (always in the local Tile.pak); the real layout is LayoutName, read in Load.</summary>
        public const string HostLayout="uistatus86";
        public static readonly Color TextColor=Color.White;

        readonly Func<LoginControl?> source=source??(()=>LoginControl.NativeEntry);

        public string Id=>PanelId;
        public UiLayer Layer=>UiLayer.Float;
        /// <summary>Why the screen cannot show; null when usable.</summary>
        public string? Unavailable{get;private set;}="not loaded";
        public bool Available=>Unavailable==null;
        public string? LayoutFile{get;private set;}
        public LoginWindow? Login{get;private set;}
        /// <summary>null when Message_Window is absent or its BaseSurf is missing (no size): refusals are only logged then.</summary>
        public LoginWindow? MessageWindow{get;private set;}
        /// <summary>Images named in the XML but not found (not drawn): "surf 6995 (LoginUI/Window[2]/Button[0]/@BaseSurf)".</summary>
        public IReadOnlyList<string> Missing{get;private set;}=[];
        /// <summary>Elements deliberately not built (other windows, run-time background, TextScroller …), by path.</summary>
        public IReadOnlyList<string> Skipped{get;private set;}=[];
        public IReadOnlyCollection<string> Unhandled=>unhandled;
        /// <summary>Caps Lock state for typed letters (injected by the offline checks).</summary>
        public Func<bool> CapsLock{get;set;}=()=>Control.IsKeyLocked(Keys.CapsLock);

        public bool Visible=>shown;
        public Rectangle Bounds=>!shown||Login==null?Rectangle.Empty:Message!=null&&MessageWindow!=null?UiRoot.Canvas:LoginBounds;
        Rectangle LoginBounds{get{var e=Login!.Extent;e.Offset(Login.Origin);return Rectangle.Intersect(e,UiRoot.Canvas);}}

        public string Account{get;private set;}="";
        public string Password{get;private set;}="";
        /// <summary>Focused input: IdInput, PasswordInput or null (keys go to the game).</summary>
        public string? Focus{get;private set;}
        /// <summary>Text of the open Message_Window, or null.</summary>
        public string? Message{get;private set;}
        /// <summary>A native login was sent and the session has not reached the character list or an end state yet.</summary>
        public bool Attempted{get;private set;}

        bool shown;LoginState? closedIn;
        LoginPiece? pressed,hover;bool pressedInside;
        readonly HashSet<string> unhandled=new(StringComparer.Ordinal);

        /// <summary>Login states in which the login screen shows (before the character list, or after the session ended).</summary>
        public static bool ShowsIn(LoginState s)=>s is LoginState.Offline or LoginState.Connecting or LoginState.AwaitSeed or LoginState.Authenticating
            or LoginState.AwaitList or LoginState.ReadingCharacters or LoginState.Rejected or LoginState.Disconnected or LoginState.Cancelled or LoginState.Faulted;

        public void Load(UiLayout host,SurfCatalog surfs){
            Clear();
            if(resources==null){Unavailable="沒有 UiResources";return;}
            UiLayout layout;
            try{layout=resources.Xml.Load(LayoutName);}
            catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"login-ui 無法載入 {LayoutName}，原生登入畫面停用（沿用既有登入入口）：{ex.Message}");return;}
            try{Load(layout,surfs.Get);}
            catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"login-ui {layout.FileName} 版面不符，原生登入畫面停用：{ex.Message}");return;}
            if(Missing.Count>0)UiResourceLog.Write($"login-ui 缺圖不畫：{string.Join("、",Missing)}");
            if(MessageWindow==null)UiResourceLog.Write($"login-ui {MessageWindowName} 不能用（沒有或底圖缺），登入失敗只記在既有登入入口");
        }

        void Clear(){
            shown=false;closedIn=null;pressed=hover=null;pressedInside=false;LayoutFile=null;Login=MessageWindow=null;Missing=[];Skipped=[];
            Account=Password="";Focus=null;Message=null;Attempted=false;Unavailable="not loaded";
        }

        /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
        internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
            Clear();
            var windows=layout.Root.ChildrenNamed(WindowNode).ToList();
            var login=windows.FirstOrDefault(w=>w.Attr("Name")==LoginWindowName)??throw new UiLayoutException(layout.FileName,layout.Root.Path,$"沒有 Window Name={LoginWindowName}");
            var message=windows.FirstOrDefault(w=>w.Attr("Name")==MessageWindowName);
            var missing=new List<string>();var skipped=new List<string>();
            foreach(var w in windows)if(w.Path!=login.Path&&w.Path!=message?.Path)skipped.Add($"{w.Path}（{w.Attr("Name")}，未做）");
            UiSurface? Resolve(LoginImageRef? r,string path){
                if(r==null)return null;
                var s=surf(r.Id);
                if(s==null)missing.Add($"{r} ({path}/@{r.Attr})");
                return s;
            }
            var built=Window(login,Resolve,skipped);
            int Inputs(string name)=>built.Pieces.Count(p=>p.Kind==LoginPieceKind.Input&&p.Name==name);
            foreach(var name in new[]{IdInput,PasswordInput})
                if(Inputs(name)!=1)throw new UiLayoutException(layout.FileName,login.Path,$"{name} 應該剛好一個（實際 {Inputs(name)}）");
            if(built.Find(IdInput)!.Password||!built.Find(PasswordInput)!.Password)throw new UiLayoutException(layout.FileName,login.Path,$"{PasswordInput} 才是 Password=\"1\"");
            LoginWindow? msg=null;
            if(message!=null){
                var m=Window(message,Resolve,skipped);
                if(m.BaseImage?.BaseSurface is {} b&&m.Pieces.Any(p=>p.Kind==LoginPieceKind.Label&&p.Name==MessageLabel))msg=m with{Size=new(b.Width,b.Height)};
            }
            LayoutFile=layout.FileName;Login=built;MessageWindow=msg;Missing=missing;Skipped=skipped;
            Unavailable=built.Extent.IsEmpty?$"{layout.FileName}: {LoginWindowName} 沒有任何可畫或可點的元素":null;
        }

        /// <summary>"6995;1" → (6995, 1); "7000" → (7000, 0). Attribute absent → null.</summary>
        public static LoginImageRef? ImageAttr(UiNode n,string attr){
            var raw=n.Attr(attr);if(raw==null)return null;
            var parts=raw.Split(';');
            static bool P(string s,out int v)=>int.TryParse(s.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out v);
            if(parts.Length>2||!P(parts[0],out int id)||id<=0)throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是圖號");
            int flag=0;if(parts.Length==2&&!P(parts[1],out flag))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 的旗標不是整數");
            return new(attr,id,flag);
        }

        static LoginWindow Window(UiNode w,Func<LoginImageRef?,string,UiSurface?> resolve,List<string> skipped){
            LoginPiece Piece(LoginPieceKind kind,UiNode n,string normalAttr){
                var normal=ImageAttr(n,normalAttr);var hi=ImageAttr(n,"HighlightSurf");var push=ImageAttr(n,"PushSurf");
                return new(kind,n.Path,n.Attr("Name"),new(n.TryInt("X")??0,n.TryInt("Y")??0),new(n.TryInt("Width")??0,n.TryInt("Height")??0),
                    normal,resolve(normal,n.Path),resolve(hi,n.Path),resolve(push,n.Path),n.Attr("MouseEvent"));
            }
            var pieces=new List<LoginPiece>();
            // Reverse XML order (see the file header): the last child is painted first.
            foreach(var c in w.Children.Reverse()){
                switch(c.Name){
                    case "Image":
                        if(ImageAttr(c,"Surf")==null){skipped.Add(c.Path+"（沒有 Surf，執行期才畫）");continue;}
                        pieces.Add(Piece(LoginPieceKind.Image,c,"Surf"));continue;
                    case "Button":
                        pieces.Add(Piece(LoginPieceKind.Button,c,"BaseSurf"));continue;
                    case "InputBox":{
                        var size=new Size(c.TryInt("Width")??0,c.TryInt("Height")??0);
                        if(size.Width<=0||size.Height<=0)throw new UiLayoutException(c.Layout.FileName,c.Path,"InputBox 沒有 Width/Height");
                        int limit=c.TryInt("LimitText")??0;
                        if(limit<=0)throw new UiLayoutException(c.Layout.FileName,c.Path,"InputBox 沒有 LimitText");
                        pieces.Add(new(LoginPieceKind.Input,c.Path,c.Attr("Name"),new(c.TryInt("X")??0,c.TryInt("Y")??0),size,null,null,null,null,null,c.Attr("Password")?.Trim()=="1",limit));
                        continue;
                    }
                    case "MultiLabel":{
                        var size=new Size(c.TryInt("Width")??0,c.TryInt("Height")??0);
                        if(size.Width<=0||size.Height<=0){skipped.Add(c.Path+"（沒有 Width/Height）");continue;}
                        pieces.Add(new(LoginPieceKind.Label,c.Path,c.Attr("Name"),new(c.TryInt("X")??0,c.TryInt("Y")??0),size,null,null,null,null,null));
                        continue;
                    }
                    default:skipped.Add(c.Path);continue;   // ScrollBar / TextScroller belong to the windows not built
                }
            }
            var baseRef=ImageAttr(w,"BaseSurf");
            LoginPiece? baseImage=baseRef==null?null:new(LoginPieceKind.Image,w.Path,w.Attr("Name"),Point.Empty,Size.Empty,baseRef,resolve(baseRef,w.Path),null,null,null);
            return new(w.Attr("Name")!,new(w.Int("X"),w.Int("Y")),new(w.TryInt("Width")??0,w.TryInt("Height")??0),baseImage,pieces);
        }

        // ---- state ----

        public void Update(IUiGameState state){
            var control=source();
            var s=control?.SessionState;
            if(s!=closedIn)closedIn=null;
            if(control==null||!Available||state.InWorld||s is not LoginState now||!ShowsIn(now)){Hide();Attempted=false;return;}
            if(Attempted){
                if(now is LoginState.Rejected or LoginState.Disconnected or LoginState.Faulted){Attempted=false;ShowMessage(ProductEntry.StateName(now));}
                else if(now==LoginState.Cancelled)Attempted=false;
            }
            if(closedIn==now){Hide();return;}
            if(!shown){shown=true;Focus=IdInput;pressed=hover=null;pressedInside=false;}
        }

        void Hide(){shown=false;pressed=hover=null;pressedInside=false;Message=null;Focus=null;}

        /// <summary>IUiClosablePanel: hides the screen until the login state changes (then it shows again).</summary>
        public void Close(){if(shown)closedIn=source()?.SessionState;Hide();}

        /// <summary>Login_Button / Enter: LoginControl.StartNative with the typed account and password (password cleared).</summary>
        public LoginAction? SubmitLogin(){
            if(!shown||source() is not {} control)return null;
            string password=Password;Password="";
            var result=control.StartNative(Account,password);
            if(result.Accepted){Attempted=true;Focus=null;}
            else ShowMessage(result.Message);
            return result;
        }

        void ShowMessage(string text){
            if(MessageWindow==null){if(unhandled.Add("message"))UiResourceLog.Write($"login-ui 訊息窗不能用，訊息只記錄：{text}");return;}
            Message=text;pressed=hover=null;pressedInside=false;
        }
        void CloseMessage(){Message=null;pressed=hover=null;pressedInside=false;Focus=Account.Length>0?PasswordInput:IdInput;}

        // ---- geometry ----

        /// <summary>The window drawn on top: Message_Window while a message shows, else Login_Window.</summary>
        LoginWindow? Active=>Message!=null&&MessageWindow!=null?MessageWindow:Login;
        public Rectangle At(LoginWindow w,LoginPiece p)=>new(w.Origin.X+p.Local.X,w.Origin.Y+p.Local.Y,p.Local.Width,p.Local.Height);
        /// <summary>Topmost clickable piece of the active window at a logical point (later in paint order = on top).</summary>
        public LoginPiece? ButtonAt(Point location){
            if(!shown||Active is not {} w)return null;
            for(int i=w.Pieces.Count-1;i>=0;i--){var p=w.Pieces[i];if(p.Clickable&&At(w,p).Contains(location))return p;}
            return null;
        }

        // ---- paint ----

        public void Paint(UiCanvas canvas){
            if(!shown||Login==null)return;
            PaintWindow(canvas,Login,Message==null);
            if(Message!=null&&MessageWindow is {} m){
                canvas.DrawSurface(m.BaseImage?.BaseSurface,m.Origin.X,m.Origin.Y);
                PaintWindow(canvas,m,true);
            }
        }

        void PaintWindow(UiCanvas canvas,LoginWindow w,bool live){
            foreach(var p in w.Pieces){
                int x=w.Origin.X+p.Offset.X,y=w.Origin.Y+p.Offset.Y;
                switch(p.Kind){
                    case LoginPieceKind.Image:canvas.DrawSurface(p.BaseSurface,x,y);break;
                    case LoginPieceKind.Button:canvas.DrawSurface(live?ButtonSurface(p):p.BaseSurface,x,y);break;
                    case LoginPieceKind.Input:PaintInput(canvas,p,x,y,live&&Focus==p.Name);break;
                    case LoginPieceKind.Label:if(p.Name==MessageLabel&&Message!=null)PaintMessage(canvas,p,x,y);break;
                }
            }
        }

        /// <summary>Push while held; Highlight while hovered; a missing state image falls back to the normal one.</summary>
        public UiSurface? ButtonSurface(LoginPiece p){
            if(p==pressed&&pressedInside&&p.PushSurface!=null)return p.PushSurface;
            if(p==hover&&p.HighlightSurface!=null)return p.HighlightSurface;
            return p.BaseSurface;
        }

        /// <summary>What an input shows: the account, or one '*' per password character.</summary>
        public string Display(LoginPiece input)=>input.Name==PasswordInput?new string('*',Password.Length):input.Name==IdInput?Account:"";

        void PaintInput(UiCanvas canvas,LoginPiece p,int x,int y,bool focused){
            var g=canvas.Graphics;string text=Display(p);
            // Longer than the box: show the tail (the caret end), never draw outside the box.
            while(text.Length>0&&UiText.Measure(g,text).Width>p.DeclaredSize.Width-2)text=text[1..];
            canvas.DrawText(text,x,y,TextColor);
            if(focused){
                int cx=x+Math.Min(UiText.Measure(g,text).Width,p.DeclaredSize.Width-1);
                using var pen=new Pen(TextColor);g.DrawLine(pen,cx,y+1,cx,y+Math.Min(p.DeclaredSize.Height,UiText.Font.Height)-1);
            }
        }

        void PaintMessage(UiCanvas canvas,LoginPiece label,int x,int y){
            var g=canvas.Graphics;
            var lines=Wrap(Message!,label.DeclaredSize.Width,s=>UiText.Measure(g,s).Width);
            int lineHeight=Math.Max(1,UiText.Font.Height);
            int maxLines=Math.Max(1,label.DeclaredSize.Height/lineHeight);
            if(lines.Count>maxLines)lines=lines.Take(maxLines).ToList();
            int top=y+Math.Max(0,(label.DeclaredSize.Height-lines.Count*lineHeight)/2);   // VAlign="center"
            for(int i=0;i<lines.Count;i++)canvas.DrawText(lines[i],x,top+i*lineHeight,TextColor);
        }

        /// <summary>Breaks text into lines no wider than width (explicit line breaks kept; character wrap, as CJK text has no spaces).</summary>
        public static List<string> Wrap(string text,int width,Func<string,int> measure){
            var lines=new List<string>();
            foreach(var para in text.Replace("\r\n","\n").Split('\n')){
                string line="";
                foreach(char ch in para){
                    string next=line+ch;
                    if(line.Length>0&&measure(next)>width){lines.Add(line);line=ch.ToString();}
                    else line=next;
                }
                lines.Add(line);
            }
            return lines;
        }

        // ---- input ----

        public bool Mouse(UiMouseEvent e,IUiCommands commands){
            if(!shown||Active is not {} w)return false;
            bool modal=Message!=null&&MessageWindow!=null;
            bool inside=modal||Bounds.Contains(e.Location);
            if(e.Kind==UiMouseKind.Move)hover=ButtonAt(e.Location);
            if(e.Kind==UiMouseKind.Wheel)return inside;
            if(e.Button!=MouseButtons.Left&&e.Kind!=UiMouseKind.Move)return inside||pressed!=null;
            switch(e.Kind){
                case UiMouseKind.Down:{
                    var hit=ButtonAt(e.Location);
                    if(hit?.Kind==LoginPieceKind.Input){Focus=hit.Name;return true;}
                    pressed=hit;pressedInside=hit!=null;
                    return inside;
                }
                case UiMouseKind.Move:
                    if(pressed!=null)pressedInside=At(w,pressed).Contains(e.Location);
                    return inside||pressed!=null;
                case UiMouseKind.Up:{
                    var was=pressed;pressed=null;pressedInside=false;
                    if(was!=null&&At(w,was).Contains(e.Location))Fire(was);
                    return inside||was!=null;
                }
                default:return inside;
            }
        }

        void Fire(LoginPiece p){
            if(Message!=null){if(p.MouseEvent is MessageOkEvent or MessageCancelEvent)CloseMessage();return;}
            if(p.MouseEvent==LoginEvent){SubmitLogin();return;}
            NotWired(p.MouseEvent??p.Name??p.Path);
        }

        void NotWired(string key){
            if(unhandled.Add(key))UiResourceLog.Write($"login-ui {key} 尚未接上");
        }

        /// <summary>
        /// Keys while an input is focused are used here (a consumed key produces no WM_CHAR, so the character is typed from the
        /// key itself, US layout); F-keys and Ctrl/Alt chords still go to the client. Without focus only the message window
        /// (Modal) takes keys.
        /// </summary>
        public bool Key(UiKeyEvent e,IUiCommands commands){
            if(!shown)return false;
            if(e.KeyCode is >=Keys.F1 and <=Keys.F24||(e.Modifiers&(Keys.Control|Keys.Alt))!=0)return false;
            if(Message!=null&&MessageWindow!=null){
                if(e.Down&&e.KeyCode is Keys.Enter or Keys.Escape)CloseMessage();
                return true;
            }
            if(Focus==null)return false;
            if(!e.Down)return true;
            switch(e.KeyCode){
                case Keys.Escape:Focus=null;commands.ReturnGameFocus();return true;
                case Keys.Tab:Focus=Focus==IdInput?PasswordInput:IdInput;return true;
                case Keys.Enter:if(Focus==IdInput)Focus=PasswordInput;else SubmitLogin();return true;
                case Keys.Back:
                    if(Focus==IdInput&&Account.Length>0)Account=Account[..^1];
                    else if(Focus==PasswordInput&&Password.Length>0)Password=Password[..^1];
                    return true;
            }
            if(KeyChar(e.KeyData,CapsLock()) is char c)Type(c);
            return true;
        }

        /// <summary>WM_CHAR for keys not consumed in Key (e.g. typed through another route): printable ASCII only (English="1").</summary>
        public bool Char(char c){
            if(!shown||Focus==null||Message!=null&&MessageWindow!=null||c is <' ' or >'~')return false;
            Type(c);return true;
        }

        void Type(char c){
            if(Login?.Find(Focus!) is not {} input)return;
            if(Focus==IdInput){if(Account.Length<input.Limit)Account+=c;}
            else if(Password.Length<input.Limit)Password+=c;
        }

        static readonly Dictionary<Keys,(char Plain,char Shift)> Oem=new(){
            [Keys.Space]=(' ',' '),[Keys.OemMinus]=('-','_'),[Keys.Oemplus]=('=','+'),[Keys.OemOpenBrackets]=('[','{'),[Keys.OemCloseBrackets]=(']','}'),
            [Keys.OemPipe]=('\\','|'),[Keys.OemSemicolon]=(';',':'),[Keys.OemQuotes]=('\'','"'),[Keys.Oemcomma]=(',','<'),[Keys.OemPeriod]=('.','>'),
            [Keys.OemQuestion]=('/','?'),[Keys.Oemtilde]=('`','~'),[Keys.Multiply]=('*','*'),[Keys.Add]=('+','+'),[Keys.Subtract]=('-','-'),
            [Keys.Divide]=('/','/'),[Keys.Decimal]=('.','.')};
        const string ShiftDigits=")!@#$%^&*(";

        /// <summary>The ASCII character a key types on a US layout, or null (not a character key).</summary>
        public static char? KeyChar(Keys keyData,bool capsLock){
            var key=keyData&Keys.KeyCode;bool shift=(keyData&Keys.Shift)!=0;
            if(key is >=Keys.A and <=Keys.Z){char c=(char)('a'+(key-Keys.A));return shift^capsLock?char.ToUpperInvariant(c):c;}
            if(key is >=Keys.D0 and <=Keys.D9)return shift?ShiftDigits[key-Keys.D0]:(char)('0'+(key-Keys.D0));
            if(key is >=Keys.NumPad0 and <=Keys.NumPad9)return (char)('0'+(key-Keys.NumPad0));
            return Oem.TryGetValue(key,out var o)?shift?o.Shift:o.Plain:null;
        }
    }
}
