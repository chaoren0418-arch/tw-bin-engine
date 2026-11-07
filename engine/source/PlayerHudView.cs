using System.Drawing.Drawing2D;
using System.ComponentModel;
namespace P06;

// Native Button semantics (keyboard + UI Automation) with an icon-first player presentation.
public sealed class PlayerHudTile:Button {
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Bitmap? Icon {get;set;}
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string Caption {get;set;}="";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string Detail {get;set;}="";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Selected {get;set;}
    public PlayerHudTile(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=PlayerHudPainter.Panel;ForeColor=Color.White;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e)=>PlayerHudPainter.Tile(e.Graphics,ClientRectangle,Font,Icon,Caption,Detail,Selected,Enabled,Focused);
    public void Describe(string name,string detail){Caption=name;Detail=detail;AccessibleName=name+"，"+detail;AccessibleDescription="按一下選擇或執行指定操作";Invalidate();}
}
public static class PlayerHudPainter {
    public static readonly Color Panel=Color.FromArgb(27,38,42),Gold=Color.FromArgb(219,188,118),Ink=Color.FromArgb(14,23,27);
    public static void Tile(Graphics g,Rectangle r,Font font,Bitmap? icon,string title,string detail,bool selected,bool enabled,bool focused=false){
        using var bg=new SolidBrush(selected?Color.FromArgb(54,66,63):Panel);g.FillRectangle(bg,r);
        using var edge=new Pen(selected||focused?Gold:Color.FromArgb(67,83,83),selected||focused?2:1);g.DrawRectangle(edge,r.X+1,r.Y+1,Math.Max(0,r.Width-3),Math.Max(0,r.Height-3));
        int inset=8;if(icon!=null){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.DrawImage(icon,new Rectangle(r.X+8,r.Y+10,32,32));inset=46;}
        TextRenderer.DrawText(g,title,font,new Rectangle(r.X+inset,r.Y+7,r.Width-inset-5,25),enabled?Color.White:Color.Silver,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g,detail,font,new Rectangle(r.X+inset,r.Y+33,r.Width-inset-5,23),selected?Gold:Color.FromArgb(176,193,189),TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
    }
    public static void Header(Graphics g,Rectangle r,Font font,PlayerHudSnapshot state){
        using var bg=new SolidBrush(Ink);g.FillRectangle(bg,r);
        int bars=Math.Min(340,Math.Max(200,r.Width/2));
        Meter(g,new(r.X+10,r.Y+7,bars-20,23),font,"HP",state.Hp,state.MaxHp,Color.FromArgb(144,45,49));
        Meter(g,new(r.X+10,r.Y+33,bars-20,23),font,"MP",state.Mp,state.MaxMp,Color.FromArgb(42,91,147));
        TextRenderer.DrawText(g,state.Name+"  "+state.Character,font,new Rectangle(r.X+bars,r.Y+6,r.Width-bars-10,25),Gold,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,state.State,font,new Rectangle(r.X+bars,r.Y+33,r.Width-bars-10,26),Color.White,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
    }
    static void Meter(Graphics g,Rectangle r,Font font,string label,uint value,uint max,Color color){
        using var bg=new SolidBrush(Color.FromArgb(39,46,49));g.FillRectangle(bg,r);
        using var fill=new SolidBrush(color);g.FillRectangle(fill,new Rectangle(r.X,r.Y,(int)(r.Width*Math.Clamp(max==0?0:(double)value/max,0,1)),r.Height));
        TextRenderer.DrawText(g,max==0?$"{label} 等待原服資料":$"{label} {value} / {max}",font,r,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
    }
}
public sealed class PlayerHudVitals:Control {
    public PlayerHudSnapshot? Snapshot {get;private set;}
    public PlayerHudVitals(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleRole=AccessibleRole.StaticText;}
    public void UpdateState(PlayerHudSnapshot state){Snapshot=state;AccessibleName=$"{state.Name}，{state.Character}，HP {state.Hp}/{state.MaxHp}，MP {state.Mp}/{state.MaxMp}，{state.State}";Invalidate();}
    protected override void OnPaint(PaintEventArgs e){if(Snapshot is {} s)PlayerHudPainter.Header(e.Graphics,ClientRectangle,Font,s);}
}
public sealed class PlayerHudView:Panel {
    readonly PlayerHudVitals vitals=new();
    readonly StatusIcons statusIcons=new();
    readonly FlowLayoutPanel shortcuts=new(){WrapContents=false,AccessibleName="快捷列 F1 至 F4"};
    readonly FlowLayoutPanel menu=new(){WrapContents=false,AccessibleName="玩家功能"};
    readonly FlowLayoutPanel grid=new(){AutoScroll=true,WrapContents=true,AccessibleName="圖示背包及技能清單"};
    readonly FlowLayoutPanel actions=new(){WrapContents=false,AccessibleName="選取項目操作"};
    readonly Label selectedText=new(){ForeColor=Color.White,AutoEllipsis=true};
    readonly Label notice=new(){ForeColor=Color.FromArgb(209,221,215),AutoEllipsis=true,AccessibleName="玩家訊息"};
    readonly Button use,assign,self,clear,chatButton;
    readonly PlayerHudTile[] slots=new PlayerHudTile[4];
    readonly Dictionary<PlayerHudBinding,PlayerHudTile> tiles=[];
    readonly ToolTip tips=new(){AutoPopDelay=15000};
    readonly PlayerHudSlots bindings=new();
    readonly PlayerHudChatPanel chat=new(){Visible=false};
    PlayerHudSnapshot? snapshot;PlayerHudBinding? selected;string drawer="",gridKey="";bool assigning;
    PlayerHudStamp lastStamp;
    public bool DrawerOpen=>drawer.Length>0;
    public bool ChatContainsFocus=>chat.Visible&&chat.ContainsFocus;
    internal bool DiagnosticChatInputFocused=>chat.DiagnosticInputFocused;
    internal string DiagnosticPanel=>!Visible?"hidden":drawer switch{"items"=>"inventory","equipment"=>"equipment","skills"=>"skills","chat"=>"chat",_=>"main"};
    internal bool ChatPanelOpen=>drawer=="chat";
    internal bool ChatButtonFocused=>chatButton.Focused;
    public event Action? ChatOpenRequested;
    public event Action? ChatEntered;
    public event Func<string,PlayerChatView,Task<bool>>? ChatSubmitRequested;
    public void UpdateChat(PlayerChatView state)=>chat.UpdateChat(state);
    public event Action? LayoutChanged;
    public event Action<PlayerHudStamp,PlayerHudBinding>? ActivateEntry;
    public event Action<PlayerHudStamp,PlayerHudBinding>? SelectEntry;
    public event Action<bool>? TargetSelfChanged;
    public event Action? MoreRequested,DiagnosticsRequested,LogoutRequested,GameFocusRequested;
    public PlayerHudView(){
        BackColor=PlayerHudPainter.Ink;AccessibleName="玩家 HUD";Font=new("Microsoft JhengHei UI",9);DoubleBuffered=true;
        use=MakeButton("使用",()=>{if(selected is {} b&&snapshot is {} s)ActivateEntry?.Invoke(s.Stamp,b);},100);
        assign=MakeButton("設為快捷",()=>{assigning=!assigning;UpdateDetails();},102);
        self=MakeButton("目標：自己",()=>{if(snapshot is {} s)TargetSelfChanged?.Invoke(!s.Self);},136);
        clear=MakeButton("清除快捷",()=>{assigning=false;clearing=!clearing;UpdateDetails();},102);
        actions.Controls.AddRange([use,assign,self,clear]);
        for(int i=0;i<4;i++){int index=i;var tile=new PlayerHudTile{Height=66,Margin=new(4,2,4,2)};tile.Click+=(_,_)=>SlotClick(index);slots[i]=tile;shortcuts.Controls.Add(tile);}
        chatButton=MakeButton("聊天",()=>ChatOpenRequested?.Invoke(),58,false);
        menu.Controls.AddRange([MakeButton("背包",()=>ToggleDrawer("items"),58),MakeButton("裝備",()=>ToggleDrawer("equipment"),58),MakeButton("技能",()=>ToggleDrawer("skills"),58),chatButton,MakeButton("服務",()=>MoreRequested?.Invoke(),62),MakeButton("診斷 F9",()=>DiagnosticsRequested?.Invoke(),80,false),MakeButton("登出",()=>LogoutRequested?.Invoke(),58,false)]);
        Controls.AddRange([vitals,shortcuts,menu,notice,grid,selectedText,actions,chat]);
        Controls.Add(statusIcons);
        MinimapUi.Attach(this,menu);
        chat.Enter+=(_,_)=>ChatEntered?.Invoke();chat.SubmitRequested+=async(text,view)=>ChatSubmitRequested!=null&&await ChatSubmitRequested(text,view);
        Resize+=(_,_)=>Arrange();
    }
    bool clearing;
    Button MakeButton(string title,Action click,int width,bool gameFocus=true){var b=new Button{Text=title,AccessibleName=title,Width=width,Height=30,Margin=new(3,2,3,2),FlatStyle=FlatStyle.Flat,BackColor=PlayerHudPainter.Panel,ForeColor=Color.White};b.FlatAppearance.BorderColor=Color.FromArgb(88,104,102);b.Click+=(_,_)=>{click();if(gameFocus)GameFocusRequested?.Invoke();};return b;}
    void ToggleDrawer(string kind){drawer=drawer==kind?"":kind;assigning=clearing=false;gridKey="";RefreshGrid();Arrange();LayoutChanged?.Invoke();}
    internal void OpenChatInput(bool byEnter){
        if(drawer!="chat"){drawer="chat";assigning=clearing=false;gridKey="";Arrange();LayoutChanged?.Invoke();}
        chat.FocusInput(byEnter);
    }
    internal void CloseChatInput(){chat.ReleaseEntryKey();if(ChatPanelOpen)CloseDrawer();}
    internal void ReleaseChatEntryKey()=>chat.ReleaseEntryKey();
    public void CloseDrawer(){if(!DrawerOpen)return;drawer="";assigning=clearing=false;Arrange();LayoutChanged?.Invoke();}
    void Arrange(){
        int d=DrawerOpen?PlayerHudGeometry.DrawerHeight:0;
        grid.SetBounds(6,4,Math.Max(1,Width-12),154);selectedText.SetBounds(10,162,Math.Max(1,Width-20),34);actions.SetBounds(4,198,Math.Max(1,Width-8),34);
        grid.Visible=selectedText.Visible=actions.Visible=DrawerOpen&&drawer!="chat";
        chat.SetBounds(0,0,Width,PlayerHudGeometry.DrawerHeight);chat.Visible=drawer=="chat";
        vitals.SetBounds(0,d,Width,64);shortcuts.SetBounds(4,d+64,Math.Max(1,Width-8),72);
        foreach(var b in slots)b.Width=Math.Max(90,(shortcuts.Width-32)/4);
        menu.SetBounds(4,d+138,Math.Max(1,Width-8),34);notice.SetBounds(10,d+174,Math.Max(1,Width-20),23);
        statusIcons.ArrangeBeside(notice,notice.Bounds);
    }
    public void UpdateState(PlayerHudSnapshot state,Func<ushort,Bitmap?> icon){
        bool identity=lastStamp.Generation!=state.Stamp.Generation||lastStamp.Actor!=state.Stamp.Actor;
        if(identity){selected=null;assigning=clearing=false;gridKey="";}
        else if(lastStamp.WorldEpoch!=state.Stamp.WorldEpoch){selected=null;assigning=clearing=false;gridKey="";}
        lastStamp=state.Stamp;snapshot=state;iconLookup=icon;bindings.Observe(state.Stamp);
        statusIcons.UpdateState((FindForm() as ClientForm)?.ReadStatusIcons()??[]);Arrange();
        // Populate explicit visible defaults once data arrives; later user choices are never overwritten.
        bindings.PopulateDefaults(state);
        vitals.UpdateState(state);tips.SetToolTip(vitals,state.Character+"\n"+state.State);
        notice.Text=state.Notice;notice.AccessibleName="玩家訊息："+state.Notice;tips.SetToolTip(notice,state.Notice);
        for(int i=0;i<4;i++){
            var entry=bindings.Resolve(i,state);var b=slots[i];b.Icon=entry?.Icon is ushort id?icon(id):null;
            string detail=entry==null?(bindings.Bindings[i]==null?"選擇項目後設為快捷":"已移除／不可用"):(state.Busy?"動作冷卻／忙碌 · ":"")+entry.Detail;
            b.Describe($"F{i+1}  {entry?.Name??"空槽"}",detail);tips.SetToolTip(b,b.AccessibleName);b.Enabled=assigning||clearing||entry is {Supported:true}&&!state.Busy;
        }
        RefreshGrid();UpdateDetails();
    }
    Func<ushort,Bitmap?> iconLookup=_=>null;
    PlayerHudEntry[] Entries=>snapshot==null?[]:drawer=="skills"?snapshot.Skills:drawer=="equipment"?snapshot.Items.Where(x=>x.Equipped||x.Detail.Contains("裝備")||x.Detail.Contains("穿")||x.Detail.Contains("卸")).ToArray():snapshot.Items;
    PlayerHudEntry? Selection=>snapshot==null||selected==null?null:snapshot.Items.Concat(snapshot.Skills).FirstOrDefault(x=>x.Binding==selected);
    void RefreshGrid(){
        if(!DrawerOpen||drawer=="chat"||snapshot==null)return;
        var entries=Entries;string key=drawer+"/"+string.Join("|",entries.Select(x=>$"{x.Binding.Kind}:{x.Binding.Id}:{x.Name}:{x.Detail}:{x.Icon}:{x.Equipped}:{x.Supported}"));
        if(gridKey==key)return;gridKey=key;grid.SuspendLayout();
        foreach(var old in grid.Controls.Cast<Control>().ToArray()){grid.Controls.Remove(old);old.Dispose();}tiles.Clear();
        foreach(var entry in entries){
            var tile=new PlayerHudTile{Width=160,Height=64,Margin=new(4),Icon=entry.Icon is ushort id?iconLookup(id):null,Selected=entry.Binding==selected};tile.Describe(entry.Name,entry.Detail);tips.SetToolTip(tile,tile.AccessibleName);
            var binding=entry.Binding;var shownStamp=snapshot.Stamp;
            tile.Click+=(_,_)=>{selected=binding;assigning=clearing=false;foreach(var pair in tiles){pair.Value.Selected=pair.Key==binding;pair.Value.Invalidate();}SelectEntry?.Invoke(shownStamp,binding);UpdateDetails();GameFocusRequested?.Invoke();};
            tiles.Add(binding,tile);grid.Controls.Add(tile);
        }
        if(entries.Length==0)grid.Controls.Add(new Label{Text=drawer=="skills"?"尚未收到已學技能。":"沒有符合的道具。",ForeColor=Color.Silver,AutoSize=true,Margin=new(10)});
        grid.ResumeLayout();
    }
    void UpdateDetails(){
        var entry=Selection;selectedText.Text=clearing?"請點選要清除的快捷槽。":assigning?"請點選 F1–F4 其中一格，指派選取項目。":entry==null?"點選圖示查看；選取不會使用。快捷槽也可直接點擊。":entry.Name+" · "+entry.Detail+(entry.Binding.Kind==PlayerHudKind.Magic?" · "+snapshot?.Target:"");
        selectedText.AccessibleName=selectedText.Text;tips.SetToolTip(selectedText,selectedText.Text);
        use.Text=entry?.Binding.Kind==PlayerHudKind.Magic?"施放一次":entry?.Equipped==true?"卸下一次":entry?.Detail.Contains("裝備")==true?"裝備一次":"使用一次";
        use.AccessibleName=use.Text+"："+entry?.Name;use.Enabled=entry is {Supported:true}&&snapshot?.Busy==false;
        assign.Enabled=entry is {Supported:true};self.Visible=drawer=="skills";self.Text=entry?.Detail.Contains("自己／")==true?(snapshot?.Self==true?"目標：自己":"目標：場景選取"):"依技能目標";self.AccessibleName=self.Text;self.Enabled=entry?.Detail.Contains("自己／")==true;
        if(entry?.Binding.Kind==PlayerHudKind.Magic&&entry.Detail.Contains("指定敵人")){selectedText.Text+=" · 請先點場景敵人，確認黃圈，再按 M／施放一次。";selectedText.AccessibleName=selectedText.Text;tips.SetToolTip(selectedText,selectedText.Text);}
        foreach(var b in slots)if(assigning||clearing)b.Enabled=true;
    }
    void SlotClick(int index){
        if(clearing){bindings.Assign(index,null);clearing=false;}
        else if(assigning&&Selection is {Supported:true} entry){bindings.Assign(index,entry.Binding);assigning=false;}
        else ActivateSlot(index);
        if(snapshot is {} state)UpdateState(state,iconLookup);GameFocusRequested?.Invoke();
    }
    public void ActivateSlot(int index){if(snapshot is {} state&&!state.Busy&&bindings.Resolve(index,state) is {Supported:true} entry)ActivateEntry?.Invoke(state.Stamp,entry.Binding);}
    protected override void Dispose(bool disposing){if(disposing){tips.Dispose();Font.Dispose();}base.Dispose(disposing);}
}
