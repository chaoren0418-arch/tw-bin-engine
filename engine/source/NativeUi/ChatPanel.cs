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
/// </summary>
public sealed class ChatPanel:IUiPanel,IUiTextInput {
    [UiPanel(20)] internal static UiPanelRegistration Registration=>new("CENTER_WINDOW","uistatus86",root=>new ChatPanel());   // ui-chat-window #468
    public const string WindowName="CENTER_WINDOW";
    public string Id=>WindowName;
    public UiLayer Layer=>UiLayer.Bar;
    public bool Visible=>inWorld;
    /// <summary>Split view is wider: LCHAT 260 + RCHAT (real 259) = 519 (推測 that the window really widens; 待驗證).</summary>
    public Rectangle Bounds=>new(Origin.X,Bottom-view.Height,view.Split?SplitWidth:Width,view.Height);
    /// <summary>Text shadow (推測; line colours come from the shared chat palette and ChatFormat runs, R2-G).</summary>
    public static readonly Color TextShadow=Color.Black,InputColor=Color.White;

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
    SurfCatalog? surfs;
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

    public void Load(UiLayout layout,SurfCatalog surfs){
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
        view=new ChatViewState(new ChatHeights(min,Math.Max(min,baseSurf?.Height??min),step));
    }

    UiSurface? Surface(Part p)=>p.Surf is int s?surfs?.Get(s):null;
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
        var p=parts[Bar];var at=At(p);int w=Surface(p)?.Width??(ChatRules.TextRight-p.Rel.X);
        int x=at.X+4,y=at.Y+2,room=Math.Max(0,w-8);
        // Show the tail that fits (caret is always at the end).
        string t=input.Text;int width=0,start=t.Length;
        while(start>0&&width+cw(t[start-1])<=room){width+=cw(t[start-1]);start--;}
        string shown=t[start..];
        canvas.DrawText(shown,x,y,InputColor);drawn.Add(new("INPUT",null,new(x,y,width,ChatRules.LineHeight),shown));
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
        if(e.KeyData==Keys.Escape&&input.Open){input.Close();UiChatGameState.Commands(commands)?.EndChatInput();return true;}
        if(e.KeyCode==Keys.Enter){
            if(!input.Open){
                if(e.KeyData!=Keys.Enter)return false;
                if(!e.Repeat)TryOpen(commands,true);
                return true; // Consume opening/repeated Enter, like ProcessPlayerChatKeyboard.
            }
            if(input.EnterDown(e.Modifiers))_=Submit(commands);
            return true;
        }
        if(input.Open&&e.KeyData==Keys.Back){input.Backspace();return true;}
        return false; // Printable keys must reach WM_CHAR → Char().
    }
    /// <summary>Typed character while the line is open: consumed even when the 106-character budget is full.</summary>
    public bool Char(char c){
        if(!inWorld||!input.Open||char.IsControl(c))return false;
        input.Type(c);return true;
    }

    async Task Submit(IUiCommands commands){
        if(input.Pending||UiChatGameState.Commands(commands) is not {} c||chat.View is not {CanSend:true} displayed||!input.Valid)return;
        string text=input.Text;input.Pending=true;
        try{
            bool sent=await c.SendChat(text,displayed);
            if(sent&&input.Text==text&&identity?.Generation==displayed.Generation&&identity.Actor==displayed.Actor)input.Clear();
        }finally{input.Pending=false;}
    }
}
