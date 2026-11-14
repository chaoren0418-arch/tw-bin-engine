namespace P06.NativeUi;

/// <summary>One thing the panel drew in the last Paint (logical coordinates); used by checks and the evidence table.</summary>
public sealed record ChatDraw(string Element,int? Surf,Rectangle Rect,string? Text);

/// <summary>
/// ui-chat-window (Issue #468, docs/design/native-ui.md §5.3): uistatus86 CENTER_WINDOW, TW (129, 470), stepSize 36.
/// Every position is read from the XML at run time (window x/y + xRPos/yRPos, or the absolute yPos of the bottom-anchored
/// bases); a missing node throws UiLayoutException, a missing surf draws (and hit-tests) nothing for that element only.
/// The window grows upward from its fixed bottom (CHAT_WINDOW yPos = 600). Elements are anchored either to that fixed base
/// (input bar, step button, channel tabs, scroll-down) or to the moving top edge (filter icons, split icon, scroll-up) — 推測.
/// The 240-high base (3279) and its border/title frame (CHAT_BORN 3280, colour-keyed interior) are drawn from the window top,
/// showing their top h rows (推測: the real 3280 has the title strip with the icons at y≈5, so its top must be the window top).
/// Lines come from the existing chat model; the input line follows PlayerChatKeyboardFlow (see ChatInputLine).
/// Not drawn (role 待驗證): scroll track tiles SCROLL1–4, SHORT_CHAT_ICON, WEBBROWSER_ICON, LANGUAGE_ICON, *_FOCUS states.
/// R10-F（T390／#905，gap-matrix 15.12.6；1:29、4:ExchangeUI）：
///   - 已驗證（引用 R7-G #809、FU7§1.5、FU9§12.1）：1028、刪除確認、交易 140 都是原版 sub_AC98E0 的**聊天輸入列提示**，不是彈出框。
///   - 引擎做法：LinePrompts（由 TradeUi.cs 接上）給目前等待答覆的提示（ChatLinePrompt）。輸入列關著時，提示畫在 CHAT_BAR 上；
///     開著時提示放在輸入文字前面。有提示時任何一列都能送出（空白＝140 的全部），答完關閉輸入列；Esc＝Cancel（不送）。
///     本機提示（Answer）直接交給交易窗，其他（刪除、1028）照常 SendChat，由 SendPlayerChat 的確認分派接手。
///   - 引擎做法：沒有提示時，CHAT_BAR 在輸入列關著時畫 PlayerChatView.Notice（playerChatNotice，引擎的狀態訊息；原版沒有這一行）。
///   - 推測（真機再確認）：提示出現時輸入列不會自動打開；按 Enter 或打字（字母、數字）時才打開，同一個 Enter 就答覆。
///     提示字色、提示與輸入之間的空白、提示過長時只畫尾端（游標在尾端）。
/// </summary>
public sealed class ChatPanel:IUiPanel,IUiTextInput,IUiImeCaret {
    /// <summary>#1271: caret of the open input line for the IME (last painted position; before the first paint, the bar's text start).</summary>
    public Rectangle? ImeCaret=>inWorld&&input.Open?inputCaret??BarCaret():null;
    Rectangle? inputCaret;
    Rectangle? BarCaret(){if(!parts.ContainsKey(Bar))return null;var (x,y,_)=BarRoom();return new(x,y,1,ChatRules.LineHeight);}
    [UiPanel(20)] internal static UiPanelRegistration Registration=>new("CENTER_WINDOW","uistatus86",root=>new ChatPanel());   // ui-chat-window #468
    public const string WindowName="CENTER_WINDOW";
    public string Id=>WindowName;
    public UiLayer Layer=>UiLayer.Bar;
    public bool Visible=>inWorld;
    /// <summary>Split view is wider: LCHAT 260 + RCHAT (real 259) = 519 (推測 that the window really widens; 待驗證).</summary>
    public Rectangle Bounds=>new(Origin.X,Bottom-view.Height,view.Split?SplitWidth:Width,view.Height);
    /// <summary>Text shadow (推測; line colours come from the shared chat palette and ChatFormat runs, R2-G).</summary>
    public static readonly Color TextShadow=Color.Black,InputColor=Color.White;
    /// <summary>R10-F：輸入列上的提示／狀態訊息字色（推測）。</summary>
    public static readonly Color PromptColor=Color.FromArgb(255,230,120),NoticeColor=Color.FromArgb(200,200,200);
    /// <summary>R10-F：目前等待答覆的聊天列提示來源（TradeUi.cs 接上；null＝沒有提示）。</summary>
    public Func<ChatLinePrompt?>? LinePrompts{get;set;}
    /// <summary>R10-F：最近一次 Update 取到的提示。</summary>
    public ChatLinePrompt? Prompt{get;private set;}

    public const string Base="CHAT_MAIN_WINDOW/CHAT_WINDOW",LeftBase="CHAT_MAIN_WINDOW/LCHAT_WINDOW",RightBase="CHAT_MAIN_WINDOW/RCHAT_WINDOW",Bar="CHAT_MAIN_WINDOW/CHAT_BAR";
    public const string Frame="CHAT_MAIN_WINDOW/CHAT_BORN",LeftFrame="CHAT_MAIN_WINDOW/LCHAT_BORN",RightFrame="CHAT_MAIN_WINDOW/RCHAT_BORN";
    public const string StepUp="CHAT_STEP_CONTROL/CHAT_UP",StepDown="CHAT_STEP_CONTROL/CHAT_DOWN";
    public const string ScrollUp="CHAT_SCROLL_CONTROL/SCROLL_UP_OFF",ScrollUpOn="CHAT_SCROLL_CONTROL/SCROLL_UP_ON",
        ScrollDown="CHAT_SCROLL_CONTROL/SCROLL_DOWN_OFF",ScrollDownOn="CHAT_SCROLL_CONTROL/SCROLL_DOWN_ON",Knob="CHAT_SCROLL_CONTROL/SCROLL_KNOB";
    public const string IconFrame="CHAT_ICON/CHAT_ICON_FRAME",SplitIcon="CHAT_ICON/CHAT_ICON_ACTIVE",SplitIconOn="CHAT_ICON/CHAT_ICON_ACTIVE_ON";
    public static string FilterPart(ChatFilter f,bool on)=>"CHAT_ICON/"+ChatRules.FilterElement(f)+(on?"_ON":"");
    public static string TabPart(ChatTab t,bool on)=>"CHANNEL_ICON/"+ChatRules.TabElement(t)+(on?"_ON":"");

    enum Anchor { Fixed, Top, Absolute }
    /// <summary>Rel = (xRPos, yRPos) or (xRPos, yPos) for Absolute. Surf-only state nodes borrow the position of their base node.</summary>
    sealed record Part(string Element,Point Rel,Anchor Anchor,int[] Surfs){public int? Surf=>Surfs.Length>0?Surfs[0]:null;}
    readonly Dictionary<string,Part> parts=new(StringComparer.Ordinal);
    Func<int,UiSurface?>? surfs;
    ChatViewState view=new(new(130,130,0));
    readonly ChatInputLine input=new();
    readonly List<ChatDraw> drawn=[];
    readonly Dictionary<char,int> charWidths=[];
    UiChatSnapshot chat=UiChatSnapshot.Empty;
    bool inWorld;long now;
    PlayerChatView? identity;long lastSequence;
    int lastTotalRows,lastVisibleRows;

    public Point Origin{get;private set;}
    /// <summary>Fixed bottom edge (CHAT_WINDOW yPos).</summary>
    public int Bottom{get;private set;}
    public int Width{get;private set;}
    public int SplitWidth{get;private set;}
    /// <summary>Left edge of the text, relative to the window: right edge of the widest channel tab + margin (推測).</summary>
    public int TextLeft{get;private set;}=ChatRules.TextLeft;
    public ChatViewState View=>view;
    public ChatInputLine Input=>input;
    public IReadOnlyList<ChatDraw> Drawn=>drawn;
    /// <summary>Optional fixed character width (offline checks); null = measured with UiText.</summary>
    public Func<char,int>? CharWidth{get;set;}
    /// <summary>Rows that fit in the left (or only) pane at the current height.</summary>
    public int VisibleRows=>Math.Max(0,(TextBottomY-TextTopY)/ChatRules.LineHeight);
    public int TotalRows=>lastTotalRows;
    int Top=>Bottom-view.Height;
    int TextTopY=>Top+ChatRules.TextTop;
    int TextBottomY=>Origin.Y+ChatRules.TextBottom;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>Same as Load(layout, catalog) with any surface source (R3-G #675: offline checks use in-memory surfaces).</summary>
    public void Load(UiLayout layout,Func<int,UiSurface?> surfs){
        this.surfs=surfs;parts.Clear();charWidths.Clear();
        var window=layout.Root.Child(WindowName);
        Origin=new(window.Int("x"),window.Int("y"));
        int step=window.Int("stepSize");
        void Add(string path,Anchor anchor,string? positionOf=null){
            var n=window.Child(path);
            if(positionOf!=null){parts[path]=new(path,parts[positionOf].Rel,parts[positionOf].Anchor,n.Surfs());return;}
            parts[path]=new(path,new(n.Int("xRPos"),anchor==Anchor.Absolute?n.Int("yPos"):n.Int("yRPos")),anchor,n.Surfs());
        }
        Add(Base,Anchor.Absolute);Add(LeftBase,Anchor.Absolute);Add(RightBase,Anchor.Absolute);
        Add(Frame,Anchor.Absolute);Add(LeftFrame,Anchor.Absolute);Add(RightFrame,Anchor.Absolute);
        Add(Bar,Anchor.Fixed);Add(StepUp,Anchor.Fixed);Add(StepDown,Anchor.Fixed);
        Add(ScrollUp,Anchor.Top);Add(ScrollUpOn,Anchor.Top,ScrollUp);Add(ScrollDown,Anchor.Fixed);Add(ScrollDownOn,Anchor.Fixed,ScrollDown);
        Add(IconFrame,Anchor.Top);Add(SplitIcon,Anchor.Top);Add(SplitIconOn,Anchor.Top);
        foreach(var f in Enum.GetValues<ChatFilter>()){Add(FilterPart(f,true),Anchor.Top);Add(FilterPart(f,false),Anchor.Top);}
        foreach(var t in Enum.GetValues<ChatTab>()){Add(TabPart(t,false),Anchor.Fixed);Add(TabPart(t,true),Anchor.Fixed,TabPart(t,false));}
        parts[Knob]=new(Knob,Point.Empty,Anchor.Fixed,window.Child(Knob).Surfs());
        Bottom=parts[Base].Rel.Y;
        int min=Bottom-Origin.Y;
        if(min<=0)throw new UiLayoutException(layout.FileName,window.Path,$"CHAT_WINDOW yPos={Bottom} 不在視窗 y={Origin.Y} 之下");
        var baseSurf=Surface(parts[Base]);
        // Width: base surf; without it, the right edge of the split bases (never invented). Max height: base surf height.
        SplitWidth=new[]{LeftBase,RightBase}.Select(p=>Surface(parts[p]) is {} s?parts[p].Rel.X+s.Width:0).Max();
        Width=baseSurf?.Width??SplitWidth;
        if(SplitWidth==0)SplitWidth=Width;
        var tabs=Enum.GetValues<ChatTab>().Select(t=>parts[TabPart(t,false)]).Select(p=>Surface(p) is {} s?p.Rel.X+s.Width:0).ToArray();
        TextLeft=tabs.Max()>0?tabs.Max()+ChatRules.PaneMargin:ChatRules.TextLeft;
        // R3-G（#675）：重新載入版面（換解析度／重讀 XML）時只換高度階，保留分頁、過濾、分割與目前高度（夾到新的階）。
        view.SetHeights(new ChatHeights(min,Math.Max(min,baseSurf?.Height??min),step));
        view.ResetScroll();
    }
    /// <summary>
    /// 高度切換（R3-G #675）：設成最接近且不超過 h 的高度階（ChatViewState.SetHeight）。
    /// 上下鈕仍是 Cycle；這是給原生 UI 宿主或設定還原用的入口。回傳實際高度。
    /// </summary>
    public int SetHeight(int h){view.SetHeight(h);view.ClampScroll(lastTotalRows,VisibleRows);return view.Height;}

    UiSurface? Surface(Part p)=>p.Surf is int s?surfs?.Invoke(s):null;
    /// <summary>Logical top-left of a non-absolute part at the current height.</summary>
    Point At(Part p)=>new(Origin.X+p.Rel.X,(p.Anchor==Anchor.Top?Top:Origin.Y)+p.Rel.Y);
    Rectangle? Rect(string path){var p=parts[path];return Surface(p) is {} s?new Rectangle(At(p),new(s.Width,s.Height)):null;}
    /// <summary>Logical position of every positioned element at the current height ("GROUP/ELEMENT" → point).</summary>
    public IReadOnlyDictionary<string,Point> Components=>parts.Values.Where(p=>p.Element!=Knob)
        .ToDictionary(p=>p.Element,p=>p.Anchor==Anchor.Absolute?new Point(Origin.X+p.Rel.X,p.Rel.Y):At(p));

    public void Update(IUiGameState state){
        inWorld=state.InWorld;now=state.Now;
        chat=inWorld?UiChatGameState.From(state):UiChatSnapshot.Empty;
        var v=chat.View;
        // New session / character (or leaving the world): like PlayerHudChatPanel.UpdateChat, drop the draft and the scroll.
        if(v?.Generation!=identity?.Generation||v?.Actor!=identity?.Actor){input.Clear();view.ResetScroll();lastSequence=0;}
        identity=v;
        if(!inWorld||v==null||(input.Open&&chat.GameFocus))input.Close();
        // R10-F：換了一個提示（新的 Key）時，輸入列給這個提示用，草稿清掉。
        var next=inWorld&&v!=null?LinePrompts?.Invoke():null;
        if(next!=null&&!Equals(next.Key,Prompt?.Key))input.Clear();
        Prompt=next;
    }

    // ---- Paint ----
    int Measure(Graphics g,char c){
        if(CharWidth!=null)return CharWidth(c);
        if(!charWidths.TryGetValue(c,out int w))charWidths[c]=w=Math.Max(1,UiText.Measure(g,c.ToString()).Width);
        return w;
    }
    public void Paint(UiCanvas canvas){
        drawn.Clear();
        var g=canvas.Graphics;Func<char,int> cw=c=>Measure(g,c);
        int h=view.Height;
        if(view.Split){BaseImage(canvas,LeftBase,h);BaseImage(canvas,RightBase,h);BaseImage(canvas,LeftFrame,h);BaseImage(canvas,RightFrame,h);}
        else{BaseImage(canvas,Base,h);BaseImage(canvas,Frame,h);}

        Image(canvas,IconFrame);
        foreach(var f in Enum.GetValues<ChatFilter>())Image(canvas,FilterPart(f,view.Enabled(f)));
        Image(canvas,view.Split?SplitIconOn:SplitIcon);
        foreach(var t in Enum.GetValues<ChatTab>())Image(canvas,TabPart(t,view.Tab==t));

        // Lines.
        var lines=chat.View?.Lines??[];
        int left=Origin.X+TextLeft,right=Origin.X+(view.Split?ChatRules.SplitX-ChatRules.PaneMargin:ChatRules.TextRight);
        var rows=view.Rows(lines,Math.Max(1,right-left),cw);
        int visible=VisibleRows;
        int added=rows.Count(r=>r.Sequence>lastSequence);
        if(lastSequence>0&&added>0)view.Arrived(added,rows.Count,visible);else view.ClampScroll(rows.Count,visible);
        lastSequence=lines.Length>0?Math.Max(lastSequence,lines[^1].Sequence):lastSequence;
        lastTotalRows=rows.Count;lastVisibleRows=visible;
        Rows(canvas,rows,view.Scroll,left,"LINE");
        if(view.Split){
            int rl=Origin.X+ChatRules.SplitX+ChatRules.PaneMargin,rr=Origin.X+SplitWidth-ChatRules.PaneMargin;
            Rows(canvas,view.Rows(lines,Math.Max(1,rr-rl),cw,rightPane:true),0,rl,"RLINE");
        }

        // Scroll buttons (ON = can scroll that way; 推測) and knob between them (推測).
        int max=ChatViewState.MaxScroll(rows.Count,visible);
        Image(canvas,view.Scroll<max?ScrollUpOn:ScrollUp);
        Image(canvas,view.Scroll>0?ScrollDownOn:ScrollDown);
        if(Rect(ScrollUp) is Rectangle su&&Rect(ScrollDown) is Rectangle sd&&Surface(parts[Knob]) is {} knob){
            int y0=su.Bottom,y1=sd.Top-knob.Height;
            if(y1>=y0){
                int y=max==0?y1:y1-(int)((long)(y1-y0)*view.Scroll/max);
                canvas.DrawSurface(knob,su.X,y);drawn.Add(new(Knob,knob.Surf,new(su.X,y,knob.Width,knob.Height),null));
            }
        }

        Image(canvas,h>=view.Heights.Steps[^1]&&view.Heights.Steps.Count>1?StepDown:StepUp);
        Image(canvas,Bar);
        if(input.Open)InputText(canvas,cw);
        else BarText(canvas,cw);
    }
    /// <summary>R10-F：輸入列關著時 CHAT_BAR 上的字：提示（PROMPT）優先，否則 playerChatNotice（NOTICE）；畫得下的開頭。</summary>
    void BarText(UiCanvas canvas,Func<char,int> cw){
        string? text=Prompt?.Text is {Length:>0} p?p:chat.View?.Notice is {Length:>0} n?n:null;
        if(text==null)return;
        var (x,y,room)=BarRoom();
        int width=0,end=0;
        while(end<text.Length&&width+cw(text[end])<=room){width+=cw(text[end]);end++;}
        string shown=text[..end];
        bool prompt=Prompt!=null;
        canvas.DrawText(shown,x,y,prompt?PromptColor:NoticeColor,TextShadow);
        drawn.Add(new(prompt?"PROMPT":"NOTICE",null,new(x,y,width,ChatRules.LineHeight),shown));
    }
    (int X,int Y,int Room) BarRoom(){
        var p=parts[Bar];var at=At(p);int w=Surface(p)?.Width??(ChatRules.TextRight-p.Rel.X);
        return (at.X+4,at.Y+2,Math.Max(0,w-8));
    }
    void BaseImage(UiCanvas canvas,string path,int h){
        var p=parts[path];if(Surface(p) is not {} s)return;
        // Window bottom fixed at yPos; the image is drawn from the window top (yPos − h) and its top h rows are visible (推測).
        int vis=Math.Min(h,s.Height);int x=Origin.X+p.Rel.X,y=p.Rel.Y-h;
        canvas.DrawSurface(s,new Rectangle(0,0,s.Width,vis),x,y);drawn.Add(new(path,p.Surf,new(x,y,s.Width,vis),null));
    }
    void Image(UiCanvas canvas,string path){
        var p=parts[path];if(Surface(p) is not {} s)return;
        var at=At(p);canvas.DrawSurface(s,at.X,at.Y);drawn.Add(new(path,p.Surf,new(at,new(s.Width,s.Height)),null));
    }
    void Rows(UiCanvas canvas,List<ChatRow> rows,int scroll,int x,string element){
        int visible=VisibleRows;
        for(int i=0;i<visible;i++){
            int index=rows.Count-1-scroll-i;if(index<0)break;
            var r=rows[index];int y=TextBottomY-(i+1)*ChatRules.LineHeight;
            if(r.Text.Length==0)continue;
            // R2-G: each coloured run (`*` colours, own colour index, `\f` codes) is drawn after the previous one's width.
            int rx=x;
            foreach(var run in r.Runs){
                if(run.Text.Length==0)continue;
                canvas.DrawText(run.Text,rx,y,ChatPalette.Color(run.Color),TextShadow);
                foreach(char ch in run.Text)rx+=Measure(canvas.Graphics,ch);
            }
            drawn.Add(new(element,null,new(x,y,0,ChatRules.LineHeight),r.Text));
        }
    }
    void InputText(UiCanvas canvas,Func<char,int> cw){
        var (x,y,room)=BarRoom();
        // R10-F: an open prompt sits in front of the typed text (sub_AC98E0); its part keeps the prompt colour.
        string prefix=Prompt?.Text is {Length:>0} pt?pt+" ":"";
        // Show the tail that fits (caret is always at the end).
        string t=prefix+input.Text;int width=0,start=t.Length;
        while(start>0&&width+cw(t[start-1])<=room){width+=cw(t[start-1]);start--;}
        string shown=t[start..];
        if(start<prefix.Length){
            string head=prefix[start..];int hw=head.Sum(c=>cw(c));
            canvas.DrawText(head,x,y,PromptColor,TextShadow);drawn.Add(new("PROMPT",null,new(x,y,hw,ChatRules.LineHeight),head));
            string typed=input.Text;
            canvas.DrawText(typed,x+hw,y,InputColor);drawn.Add(new("INPUT",null,new(x+hw,y,width-hw,ChatRules.LineHeight),typed));
        }
        else{canvas.DrawText(shown,x,y,InputColor);drawn.Add(new("INPUT",null,new(x,y,width,ChatRules.LineHeight),shown));}
        inputCaret=new(x+width,y,1,ChatRules.LineHeight);   // #1271: IME composition / candidate anchor (blink-independent)
        if(now/ChatRules.CaretBlinkMs%2==0){
            using var pen=new Pen(InputColor);canvas.Graphics.DrawLine(pen,x+width,y,x+width,y+ChatRules.LineHeight-2);
            drawn.Add(new("CARET",null,new(x+width,y,1,ChatRules.LineHeight-1),null));
        }
    }

    // ---- Input ----
    bool Hit(string path,Point at)=>Rect(path) is Rectangle r&&r.Contains(at);
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(e.Kind==UiMouseKind.Wheel){
            if(e.Delta!=0)view.ScrollBy(e.Delta>0?ChatRules.WheelRows:-ChatRules.WheelRows,lastTotalRows,lastVisibleRows);
            return true;
        }
        if(e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return true;
        var at=e.Location;
        if(Hit(StepUp,at)||Hit(StepDown,at)){view.Cycle();return true;}
        if(Hit(ScrollUp,at)){view.ScrollBy(1,lastTotalRows,lastVisibleRows);return true;}
        if(Hit(ScrollDown,at)){view.ScrollBy(-1,lastTotalRows,lastVisibleRows);return true;}
        if(Hit(SplitIcon,at)||Hit(SplitIconOn,at)){view.Split=!view.Split;view.ResetScroll();return true;}
        foreach(var f in Enum.GetValues<ChatFilter>())if(Hit(FilterPart(f,view.Enabled(f)),at)){view.Toggle(f);view.ResetScroll();return true;}
        foreach(var t in Enum.GetValues<ChatTab>())if(Hit(TabPart(t,view.Tab==t),at)){view.Tab=t;view.ResetScroll();return true;}
        // Clicking the input bar opens the line (legacy ChatOpenRequested → OpenPlayerChat(false)).
        if(Hit(Bar,at)&&!input.Open)TryOpen(commands,false);
        return true;
    }
    bool TryOpen(IUiCommands commands,bool byEnter){
        if(UiChatGameState.Commands(commands) is not {} c||chat.View is not {CanSend:true})return false;
        if(!c.BeginChatInput())return false;
        input.Begin(byEnter);return true;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!inWorld)return false;
        if(!e.Down){
            if(e.KeyCode==Keys.Enter)input.EnterUp();
            return false;
        }
        // R10-F: Esc while a prompt waits = cancel the prompt (nothing sent), and close the line if it was open.
        if(e.KeyData==Keys.Escape&&Prompt is {} asked&&!e.Repeat){
            Prompt=null;asked.Cancel?.Invoke();
            if(input.Open){input.Clear();input.Close();UiChatGameState.Commands(commands)?.EndChatInput();}
            return true;
        }
        if(e.KeyData==Keys.Escape&&input.Open){input.Close();UiChatGameState.Commands(commands)?.EndChatInput();return true;}
        if(e.KeyCode==Keys.Enter){
            if(!input.Open){
                if(e.KeyData!=Keys.Enter)return false;
                if(Prompt!=null){
                    // R10-F: the prompt already owns the line; the same Enter opens it and answers (blank = 140's "all").
                    if(!e.Repeat&&TryOpen(commands,false)&&input.EnterDown(e.Modifiers))_=Submit(commands);
                    return true;
                }
                if(!e.Repeat)TryOpen(commands,true);
                return true; // Consume opening/repeated Enter, like ProcessPlayerChatKeyboard.
            }
            if(input.EnterDown(e.Modifiers))_=Submit(commands);
            return true;
        }
        if(input.Open&&e.KeyData==Keys.Back){input.Backspace();return true;}
        // R10-F: typing a letter or digit while a prompt waits opens the line; the key itself still goes on to WM_CHAR → Char().
        if(!input.Open&&Prompt!=null&&!e.Repeat&&PromptOpensOn(e.KeyData))TryOpen(commands,false);
        return false; // Printable keys must reach WM_CHAR → Char().
    }
    /// <summary>R10-F（推測）：提示等待中，哪些鍵會打開輸入列：不帶 Ctrl／Alt 的字母、數字、數字鍵盤數字。</summary>
    public static bool PromptOpensOn(Keys keyData){
        if((keyData&(Keys.Control|Keys.Alt))!=0)return false;
        var k=keyData&Keys.KeyCode;
        return k is >=Keys.A and <=Keys.Z or >=Keys.D0 and <=Keys.D9 or >=Keys.NumPad0 and <=Keys.NumPad9;
    }
    /// <summary>Typed character while the line is open: consumed even when the 106-character budget is full.</summary>
    public bool Char(char c){
        if(!inWorld||!input.Open||char.IsControl(c))return false;
        input.Type(c);return true;
    }

    async Task Submit(IUiCommands commands){
        if(input.Pending||UiChatGameState.Commands(commands) is not {} c)return;
        var asked=Prompt;
        if(asked is {Answer:{} answer}){
            // R10-F: local prompt (trade 140) — the answer goes straight to its owner, never to the chat send path. One answer only.
            string typed=input.Text;Prompt=null;input.Clear();input.Close();c.EndChatInput();
            answer(typed);return;
        }
        if(chat.View is not {CanSend:true} displayed||!input.ValidFor(asked))return;
        string text=input.Text;input.Pending=true;
        try{
            // R10-F: with a prompt (item delete, private shop 1028) SendPlayerChat treats this line as the answer.
            bool sent=await c.SendChat(text,displayed);
            bool same=identity?.Generation==displayed.Generation&&identity.Actor==displayed.Actor;
            if(sent&&input.Text==text&&same)input.Clear();
            if(asked!=null&&sent&&same&&input.Open){input.Close();c.EndChatInput();}
        }finally{input.Pending=false;}
    }
}
