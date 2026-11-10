namespace P06.NativeUi;

/// <summary>
/// PARTY_INTERFACE (Issue #471; docs/design/native-ui.md §5.3 ui-party; docs/reference/ui-layout.md §6, uiextra86-c.xml).
/// Three tabs on top of LEFT_WINDOW at y=481 (character 3205 x=11, party 3208 x=36, pet 3298 x=65). The character tab shows the
/// left status frame untouched (this panel then only owns the tab strip); the party tab lays base 3204 at (0,493) over the lower
/// 107 px of LEFT_WINDOW with the 7 member cells and leave/ban/delegate at y=574 (x=70/90/109); the pet tab lays 3301 there.
/// Must paint after LEFT_WINDOW (same bar layer: registration order decides, see UiPanelRegistry).
/// </summary>
public sealed class PartyPanel:IUiPanel {
    [UiPanel(90)] internal static UiPanelRegistration Registration=>new(PartyPanel.WindowId,"uiextra86",root=>new PartyPanel(root.SlotGroup(UiSlotGroup.Bar485)));   // ui-party #471 (keep AFTER LEFT_WINDOW: the party base covers it)
    public const string WindowId="PARTY_INTERFACE";
    public enum Page { Character=0, Party=1, Pet=2 }
    /// <summary>
    /// Member cells: 2 columns × 4 rows of 64×27 (the 3219 cursor size), cell k=0..6 row-major ("Party 1".."Party 7" in the
    /// 3204 artwork), the 8th cell holds the three buttons. Origin and pitch were measured from the divider lines of the real
    /// 3204 image (columns 4..67 / 68..131, rows every 26 px from 0); the BIN drawing code is not traced (推測).
    /// </summary>
    public static readonly Size CellOrigin=new(4,0),CellPitch=new(64,26);

    static readonly (Page Page,string SurfName)[] TabNames=[(Page.Character,"SURF_800_PARTY_BTN_CHAR_STATUS"),(Page.Party,"SURF_800_PARTY_BTN_PARTY_STATUS"),(Page.Pet,"SURF_800_PARTY_LT_BTN_PET_STATUS")];
    PartyLayout.Button[] tabs=[];
    PartyLayout.Button[] buttons=[];
    UiSurface? partyBase,petBase,cursor;
    PartyLayout.Gauge gauge=new(null,null);
    Point baseAt,petAt;
    readonly UiSlotGroup? bar485;
    readonly PartyLayout.Interaction act=new();
    PartyView? party;bool inWorld;

    /// <summary>bar485: the BAR_485 slot group cycled by Alt+F3 (V5 (e)); null = no cycling from this panel.</summary>
    public PartyPanel(UiSlotGroup? bar485=null){this.bar485=bar485;}
    public string Id=>WindowId;
    public UiLayer Layer=>UiLayer.Bar;
    public Page Current{get;private set;}=Page.Character;
    public bool Visible=>inWorld&&party!=null&&(tabs.Any(t=>t.Normal!=null)||partyBase!=null);
    /// <summary>Character tab: only the tab strip (LEFT_WINDOW stays clickable). Party/pet tab: tab strip ∪ the 135×107 base.</summary>
    public Rectangle Bounds=>Current==Page.Character?TabStrip:PartyLayout.Union(TabStrip,BaseRect);
    public Rectangle TabStrip{get;private set;}
    public Rectangle BaseRect=>PartyLayout.Rect(baseAt,partyBase??petBase);
    public IReadOnlyList<Rectangle> TabRects=>tabs.Select(t=>t.Rect).ToArray();
    public IReadOnlyList<Rectangle> ButtonRects=>buttons.Select(b=>b.Rect).ToArray();
    public string? Selected=>act.Selected;
    public Rectangle Cell(int k)=>PartyLayout.Cell(baseAt+CellOrigin,CellPitch,k);
    public Rectangle GaugeRect(int k)=>PartyLayout.GaugeRect(Cell(k),gauge);
    public Rectangle FillRect(int k,byte? hp)=>PartyLayout.FillRect(Cell(k),gauge,hp);

    public void Load(UiLayout layout,SurfCatalog surfs){
        var w=layout.Root.Child(WindowId);int x=w.Int("x"),y=w.Int("y");
        var baseNode=PartyLayout.Placed(w,"SURF_IMAGE","SURF_800_PARTY_BASE");
        baseAt=new(x+baseNode.Int("xPos"),y+baseNode.Int("yPos"));partyBase=surfs.Get(PartyLayout.FirstSurf(baseNode));
        var pet=PartyLayout.Find(w,"IMAGE","T_IMG_PARTY_LT_PET_WIN");petAt=new(x+pet.Int("xPos"),y+pet.Int("yPos"));petBase=surfs.Get(PartyLayout.FirstSurf(pet));
        tabs=TabNames.Select(t=>PartyLayout.LoadButton(PartyLayout.Find(w,"STAGE3_BTN",t.SurfName),x,y,surfs,(int)t.Page)).ToArray();
        buttons=PartyLayout.LoadActions(w,x,y,surfs);
        gauge=PartyLayout.LoadGauge(w,surfs);
        cursor=surfs.Get(PartyLayout.FirstSurf(PartyLayout.Find(w,"IMAGE","SURF_800_PARTY_CURSOR_SELECTED")));
        TabStrip=tabs.Aggregate(Rectangle.Empty,(r,t)=>PartyLayout.Union(r,t.Rect));
    }

    public void Update(IUiGameState state){
        inWorld=state.InWorld;party=inWorld?PartyStates.StateOf(state)?.Party:null;
        act.Sync(party);
    }

    public void SetPage(Page page){Current=page;act.Release();}

    public void Paint(UiCanvas canvas){
        if(party==null)return;
        if(Current==Page.Party){
            canvas.DrawSurface(partyBase,baseAt.X,baseAt.Y);
            PartyLayout.PaintMembers(canvas,party,gauge,cursor,act.Selected,Cell);
            foreach(var b in buttons)canvas.DrawSurface(act.Pressed==b.Tag?b.Down:b.Normal,b.Rect.X,b.Rect.Y);
        }else if(Current==Page.Pet)canvas.DrawSurface(petBase,petAt.X,petAt.Y);   // pet list itself is not part of #471 (report)
        foreach(var t in tabs)canvas.DrawSurface(t.Tag==(int)Current?t.Down:t.Normal,t.Rect.X,t.Rect.Y);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(party==null||e.Button!=MouseButtons.Left)return false;
        // R10-D（T388／#903）：不是在本窗按下的左鍵放開＝從場景拖人進來（UiInput 只把沒被別的面板捕捉的放開交給命中的面板）。
        if(e.Kind==UiMouseKind.Up&&!ownPress&&WorldObjectDrag.Take() is uint){DropInvite(commands);return true;}
        if(e.Kind==UiMouseKind.Down)WorldObjectDrag.Clear();
        bool handled=MouseCore(e,commands);
        if(e.Kind==UiMouseKind.Down)ownPress=handled;else if(e.Kind==UiMouseKind.Up)ownPress=false;
        return handled;
    }
    bool ownPress;
    bool MouseCore(UiMouseEvent e,IUiCommands commands){
        if(e.Kind==UiMouseKind.Down)foreach(var t in tabs)if(!t.Rect.IsEmpty&&t.Rect.Contains(e.Location)){SetPage((Page)t.Tag);return true;}
        if(Current!=Page.Party)return e.Kind==UiMouseKind.Down&&Bounds.Contains(e.Location);
        return act.Mouse(e,party!,buttons,Cell,commands);
    }

    /// <summary>
    /// R10-D（T388／#903，FU9§9.1）：拖人到隊伍窗（原版 sub_ED8AC0 收到 517 → type 0／1）。只在隊伍分頁、隊伍快照可操作且可邀請時，
    /// 走既有 PartyAction("invite")（ClientForm.PartyAction → SendParty：對象＝拖曳開始時選中的玩家 World.TargetId，type 0）。
    /// 對象是否為玩家、是不是自己、隊長資格等由 SendParty 再擋一次，伺服器回 416／417 等。回傳是否交出邀請。
    /// </summary>
    public bool DropInvite(IUiCommands commands){
        if(Current!=Page.Party||party is not {Available:true,CanInvite:true} shown||PartyStates.CommandsOf(commands) is not {} cmd)return false;
        cmd.PartyAction("invite",shown,"");return true;
    }

    /// <summary>
    /// Alt+F3 cycles the BAR_485 slot (minimap → extended macro → party/pet RT, V5 (e) verified in TW13). Lives here because this
    /// panel is visible whenever the player is in the world, while the slot members themselves are hidden when not current.
    /// Whether WinForms delivers Alt+F3 (WM_SYSKEYDOWN) to the native key path is not verified (needs the ui-native-smoke run).
    /// </summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(bar485==null||!inWorld||e.KeyData!=(Keys.Alt|Keys.F3))return false;
        if(e.Down&&!e.Repeat)bar485.Cycle();
        return true;
    }
}

/// <summary>Shared layout, paint and click logic of the two party panels.</summary>
public static class PartyLayout {
    public sealed record Button(int Tag,Rectangle Rect,UiSurface? Normal,UiSurface? Down);
    public sealed record Gauge(UiSurface? Frame,UiSurface? Fill);
    public const int MemberCells=7,Columns=2;
    /// <summary>Button tags of leave / ban / delegate (STAGE3_BTN order in the XML).</summary>
    public const int Leave=0,Ban=1,Delegate=2;
    static readonly string[] ActionNames=["SURF_800_PARTY_BTN_PARTY_LEAVE_OFF","SURF_800_PARTY_BTN_PARTY_BAN_OFF","SURF_800_PARTY_BTN_PARTY_DELEGATE_OFF"];
    /// <summary>
    /// STAGE3_BTN holds 3 images (a, a+1, a+2). LImageButton3Stage draws base+1 while pressed (V4, verified for IMAGE_BUTTON/3Stage);
    /// that the STAGE3_BTN element uses the same rule, and that a selected tab looks pressed, is 推測.
    /// </summary>
    public const int PressedOffset=1;
    /// <summary>Gauge 3211 (60×10) inside a 64×27 cell, red 3212 (58×8) one pixel inside it. Position inside the cell is 推測 (below the name).</summary>
    public static readonly Size GaugeOffset=new(2,15),FillOffset=new(1,1),NameOffset=new(3,2);
    public static readonly Color NameColor=Color.White,NameShadow=Color.Black;

    public static int FirstSurf(UiNode n){var s=n.Surfs();return s.Length>0?s[0]:throw new UiLayoutException(n.Layout.FileName,n.Path,"surfNumber 是空的");}
    public static Rectangle Rect(Point at,UiSurface? s)=>s==null?Rectangle.Empty:new(at,new(s.Width,s.Height));
    public static Rectangle Union(Rectangle a,Rectangle b)=>a.IsEmpty?b:b.IsEmpty?a:Rectangle.Union(a,b);

    /// <summary>The unique child element with this surfName (the XML repeats element names, so names alone are ambiguous).</summary>
    public static UiNode Find(UiNode w,string element,string surfName){
        var m=w.ChildrenNamed(element).Where(n=>n.Attr("surfName")==surfName).ToArray();
        return m.Length==1?m[0]:throw new UiLayoutException(w.Layout.FileName,w.Path+"/"+element,$"surfName {surfName} 應恰有 1 個，實際 {m.Length}");
    }
    /// <summary>The SURF_IMAGE that carries xPos/yPos (the XML lists the base twice; the second copy has no position).</summary>
    public static UiNode Placed(UiNode w,string element,string surfName){
        var m=w.ChildrenNamed(element).Where(n=>n.Attr("surfName")==surfName&&n.Attr("xPos")!=null).ToArray();
        return m.Length==1?m[0]:throw new UiLayoutException(w.Layout.FileName,w.Path+"/"+element,$"有座標的 {surfName} 應恰有 1 個，實際 {m.Length}");
    }
    public static Button LoadButton(UiNode n,int x,int y,SurfCatalog surfs,int tag){
        int a=FirstSurf(n);var normal=surfs.Get(a);
        return new(tag,Rect(new(x+n.Int("xRPos"),y+n.Int("yRPos")),normal),normal,surfs.Get(a+PressedOffset));
    }
    public static Button[] LoadActions(UiNode w,int x,int y,SurfCatalog surfs)=>ActionNames.Select((name,i)=>LoadButton(Find(w,"STAGE3_BTN",name),x,y,surfs,i)).ToArray();
    public static Gauge LoadGauge(UiNode w,SurfCatalog surfs)=>new(
        surfs.Get(FirstSurf(Find(w,"IMAGE","SURF_800_PARTY_STATUS_GAUGE")) ),
        surfs.Get(FirstSurf(Find(w,"IMAGE","SURF_800_PARTY_STATUS_GAUGE_RED"))));

    public static Rectangle Cell(Point origin,Size pitch,int k)=>k is <0 or >=MemberCells?Rectangle.Empty:new(origin.X+pitch.Width*(k%Columns),origin.Y+pitch.Height*(k/Columns),64,27);
    public static int CellAt(Func<int,Rectangle> cell,Point p){for(int k=0;k<MemberCells;k++)if(cell(k).Contains(p))return k;return -1;}
    public static Rectangle GaugeRect(Rectangle cell,Gauge g)=>cell.IsEmpty?Rectangle.Empty:Rect(cell.Location+GaugeOffset,g.Frame);
    /// <summary>
    /// Red bar = the server's HP ratio over the fill image width (58 px): floor(58·hp/100). HP not sent (null, e.g. a member only
    /// known from the S39 name list) → no fill at all, never a guessed value. 0 % is a valid member with an empty bar.
    /// </summary>
    public static int FillWidth(int fullWidth,byte? hp)=>hp is byte v?fullWidth*Math.Min((int)v,100)/100:0;
    public static Rectangle FillRect(Rectangle cell,Gauge g,byte? hp){
        if(cell.IsEmpty||g.Fill==null)return Rectangle.Empty;
        int w=FillWidth(g.Fill.Width,hp);
        return w<=0?Rectangle.Empty:new(cell.Location+GaugeOffset+FillOffset,new(w,g.Fill.Height));
    }

    /// <summary>Members in the snapshot order (Party.cs orders by name; the original join order is not in the model), at most 7.</summary>
    public static void PaintMembers(UiCanvas canvas,PartyView party,Gauge g,UiSurface? cursor,string? selected,Func<int,Rectangle> cell){
        for(int k=0;k<Math.Min(party.Members.Length,MemberCells);k++){
            var m=party.Members[k];var c=cell(k);
            canvas.DrawText(m.Name,c.X+NameOffset.Width,c.Y+NameOffset.Height,NameColor,NameShadow);
            var gr=GaugeRect(c,g);canvas.DrawSurface(g.Frame,gr.X,gr.Y);
            var f=FillRect(c,g,m.HpPercent);if(!f.IsEmpty)canvas.DrawSurface(g.Fill,new Rectangle(0,0,f.Width,f.Height),f.X,f.Y);
            if(m.Name==selected)canvas.DrawSurface(cursor,c.X,c.Y);
        }
    }

    /// <summary>Selection and the three action buttons (click fires on release inside the pressed button, like LImageButton).</summary>
    public sealed class Interaction {
        public string? Selected{get;private set;}
        public int? Pressed{get;private set;}
        public void Release()=>Pressed=null;
        public void Sync(PartyView? party){if(Selected!=null&&(party==null||!party.Members.Any(m=>m.Name==Selected)))Selected=null;}
        public void Select(string? name)=>Selected=name;

        public bool Mouse(UiMouseEvent e,PartyView party,Button[] buttons,Func<int,Rectangle> cell,IUiCommands commands){
            var hit=buttons.FirstOrDefault(b=>!b.Rect.IsEmpty&&b.Rect.Contains(e.Location));
            if(e.Kind==UiMouseKind.Down){
                if(hit!=null){Pressed=hit.Tag;return true;}
                int k=CellAt(cell,e.Location);
                if(k>=0)Selected=k<party.Members.Length?party.Members[k].Name:null;
                return true;
            }
            if(e.Kind!=UiMouseKind.Up)return true;
            int? pressed=Pressed;Pressed=null;
            if(pressed==null||hit?.Tag!=pressed)return true;
            Fire(pressed.Value,party,commands);
            return true;
        }

        /// <summary>
        /// Leave → existing "leave"; ban → existing "kick" with the selected member (only for the leader, same rule as SendParty);
        /// delegate → "delegate" with the selected member (R7-J #812: C230 type=3 leader transfer, docs/protocol/party.md:21); only for
        /// the leader and a member whose object id is known (PartyPackets.DelegateTarget). No confirmation prompt (推測: BIN not traced).
        /// </summary>
        public bool Fire(int tag,PartyView party,IUiCommands commands){
            if(PartyStates.CommandsOf(commands) is not {} cmd||!party.Available)return false;
            switch(tag){
                case Leave when party.Members.Length>0:cmd.PartyAction("leave",party,"");return true;
                case Ban when party.CanKick&&Selected is {} name&&party.Members.Any(m=>m.Name==name):cmd.PartyAction("kick",party,name);return true;
                case Delegate when Selected is {} who&&PartyPackets.DelegateTarget(party,who)!=0:cmd.PartyAction("delegate",party,who);return true;
                default:return false;
            }
        }
    }
}
