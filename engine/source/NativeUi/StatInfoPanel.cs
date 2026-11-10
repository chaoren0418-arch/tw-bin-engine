namespace P06.NativeUi;

// T325 (#763, P-D of docs/reference/pak-resources.md §6, row 4:StatInfoUI): the native stat bonus window CStatInfoUI.
// TW16 reads StatInfoUI.xml; 0-客戶端 does not ship it (PAK§4.2). It is read at run time from the reference client through
// UiResources.Shared.Xml (T319/#757; SHA-256 logged as "ui-xml-ref"). Nothing is copied into the repository.
// PAK§4.1 counted 0 surfs: the layout has no Surf attribute, its only image is BasePng 10911 (local Sprite: none; kor25
// Sprite02 "10911c.png"), read through OptionPngSource. A missing image is not drawn and listed in Missing.
//
// Loading. Host layout "uistatus86" (like OptionPanel / RenewalStatusPanel); Load reads StatInfoUI itself. No reference
// client / no XML / malformed XML / no base image (no window size) → Unavailable: the window never opens. Before this
// task StatInfoUI had no window at all (RenewalStatusPanel only toggled its check image), so nothing else changes.
//
// Layout rules (推測; the binder was not decompiled for this):
// - One Window BonusStatusTooltip_Window. Size = BasePng image. Moveable="340" = drag strip height (the whole window).
// - List Vertical="1": children stacked top to bottom from the List origin, each advancing by its declared Height.
//   Coordinates inside are relative to the parent; a missing X/Y is 0.
// - "*_Label" Labels with String ids go through TextResolver (null in the product: the reference ids would not match the
//   local string-c.tbl, see #768). "*_Value" Labels show ValueProvider(name); the engine has no bonus values (damage,
//   hit, critical, carry, ER, MR …) yet, so in the product they are blank — the design-time Caption "0" is never drawn (待做).
// - Opening: RenewalStatusPanel's StatusBonus_Button (MouseEvent ShowStatInfoTooltip) is a CheckButton. When it becomes
//   checked while the status window is open, this window opens next to the status window's right edge (kept inside
//   800×600); unchecking it or closing the status window closes this one. Esc (KeyboardEvent CloseStatInfoWindow) closes.

public sealed record StatInfoRow(string Name,string? LabelName,string? LabelKind,string? LabelValue,string? ValueName,Point LabelOffset,Size LabelSize,
    Point ValueOffset,Size ValueSize,Color LabelColor,Color ValueColor,string ValueHAlign,string ValueVAlign,string LabelVAlign);

public sealed class StatInfoPanel(UiResources? resources=null,UiRoot? root=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(182)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,r=>new StatInfoPanel(r.Resources,r));   // T325 (#763): StatInfoUI.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="StatInfoUI";
    public const string LayoutName="StatInfoUI",RootNode="StatInfoUI",WindowName="BonusStatusTooltip_Window",HostLayout="uistatus86";

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Point LayoutOrigin{get;private set;}
    public Size Size{get;private set;}
    public Rectangle Bounds=>open&&!Size.IsEmpty?new(Origin,Size):Rectangle.Empty;
    public bool Visible=>open;
    public UiSurface? BaseImage{get;private set;}
    public int MoveableHeight{get;private set;}
    public IReadOnlyList<StatInfoRow> Rows{get;private set;}=[];
    public IReadOnlyList<string> Missing{get;private set;}=[];
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    public Func<string,string,string?>? TextResolver{get;set;}
    /// <summary>"DmgBonus_Value" → text; null provider or null result = blank (the product has none yet).</summary>
    public Func<string,string?>? ValueProvider{get;set;}
    bool open,followed;

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"stat-info-ui 無法載入 {LayoutName}，能力資訊窗停用：{ex.Message}");return;}
        var png=new OptionPngSource(resources.Sprites,resources.Xml.Reference);
        try{Load(layout,surfs.Get,png.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"stat-info-ui {layout.FileName} 版面不符，能力資訊窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"stat-info-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"stat-info-ui {Unavailable}");
    }

    void Clear(){open=false;followed=false;LayoutFile=null;Origin=LayoutOrigin=Point.Empty;Size=Size.Empty;BaseImage=null;MoveableHeight=0;Rows=[];Missing=[];Skipped=[];Unavailable="not loaded";}

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
        Clear();
        var r=layout.Root;
        if(r.Name!=RootNode)throw new UiLayoutException(layout.FileName,r.Path,$"根元素不是 {RootNode}");
        var missing=new List<string>();var skipped=new List<string>();var rows=new List<StatInfoRow>();
        UiNode? window=null;
        foreach(var w in r.Children){
            if(w.Name=="Window"&&w.Attr("Name")==WindowName){
                if(window!=null)throw new UiLayoutException(layout.FileName,w.Path,$"Window {WindowName} 重複");
                window=w;
            }else skipped.Add(w.Path);
        }
        if(window==null)throw new UiLayoutException(layout.FileName,r.Path,$"沒有 {WindowName}");
        var baseRef=ResetStatPanel.ImageRef(window,"BasePng")??ResetStatPanel.ImageRef(window,"BaseSurf");
        UiSurface? baseSurface=null;
        if(baseRef!=null){baseSurface=baseRef.IsSurf?surf(baseRef.Id):png(baseRef.Id);if(baseSurface==null)missing.Add($"{baseRef} ({window.Path}/@{baseRef.Attr})");}
        foreach(var c in window.Children){
            if(c.Name!="List"){skipped.Add(c.Path);continue;}
            var at=new Point(c.TryInt("X")??0,c.TryInt("Y")??0);
            bool vertical=c.Attr("Vertical")?.Trim()=="1";
            int advance=0;
            foreach(var k in c.Children){
                if(k.Name!="Control"){skipped.Add(k.Path);continue;}
                var origin=new Point(at.X+(k.TryInt("X")??0),at.Y+(k.TryInt("Y")??0)+(vertical?advance:0));
                if(vertical)advance+=k.TryInt("Height")??throw new UiLayoutException(layout.FileName,k.Path,"直式 List 的列沒有 Height");
                rows.Add(Row(k,origin,skipped));
            }
        }
        if(rows.Count==0)throw new UiLayoutException(layout.FileName,window.Path,"沒有任何列");
        LayoutFile=layout.FileName;BaseImage=baseSurface;Size=baseSurface==null?Size.Empty:new(baseSurface.Width,baseSurface.Height);
        LayoutOrigin=Origin=new(window.TryInt("X")??0,window.TryInt("Y")??0);MoveableHeight=window.TryInt("Moveable")??0;
        Rows=rows;Missing=missing;Skipped=skipped;
        Unavailable=Size.IsEmpty?$"{layout.FileName}: 底圖缺，無法定出視窗範圍":null;
    }

    static StatInfoRow Row(UiNode control,Point origin,List<string> skipped){
        UiNode? label=null,value=null;
        foreach(var l in control.Children){
            string? n=l.Name=="Label"?l.Attr("Name"):null;
            if(n!=null&&n.EndsWith("_Value",StringComparison.Ordinal)&&value==null)value=l;
            else if(n!=null&&n.EndsWith("_Label",StringComparison.Ordinal)&&label==null)label=l;
            else skipped.Add(l.Path);
        }
        static Point P(UiNode? n,Point o)=>n==null?o:new(o.X+(n.TryInt("X")??0),o.Y+(n.TryInt("Y")??0));
        static Size S(UiNode? n)=>n==null?Size.Empty:new(n.TryInt("Width")??0,n.TryInt("Height")??0);
        string? kind=label?.Attr("Desc")!=null?"Desc":label?.Attr("String")!=null?"String":null;
        return new(control.Attr("Name")??control.Path,label?.Attr("Name"),kind,kind==null?null:label!.Attr(kind)!.Trim(),value?.Attr("Name"),P(label,origin),S(label),P(value,origin),S(value),
            label==null?Color.White:ResetStatPanel.ParseColor(label,"FontColor",Color.White),value==null?Color.White:ResetStatPanel.ParseColor(value,"FontColor",Color.White),
            (value?.Attr("HAlign")??value?.Attr("Align")??"left").Trim().ToLowerInvariant(),(value?.Attr("VAlign")??"top").Trim().ToLowerInvariant(),(label?.Attr("VAlign")??"top").Trim().ToLowerInvariant());
    }

    public bool Open(){if(!Available)return false;open=true;return true;}
    /// <summary>Opens next to a window (its right edge, same top), kept inside 800×600.</summary>
    public bool OpenBeside(Rectangle anchor){
        if(!Available)return false;
        Origin=UiInput.ClampInside(new(anchor.Right,anchor.Top),Size);open=true;return true;
    }
    public void Close()=>open=false;
    public bool Toggle(){if(open){Close();return true;}return Open();}

    public void Update(IUiGameState state){
        if(!state.InWorld){if(open)Close();followed=false;return;}
        if(root?.Find(RenewalStatusPanel.PanelId) is not RenewalStatusPanel status)return;
        bool want=status.Visible&&status.StatInfoChecked&&!root.Faulted.Contains(status);
        if(want&&!followed)OpenBeside(status.Bounds);
        else if(!want&&followed&&open)Close();
        followed=want;
    }

    public string? LabelText(StatInfoRow r)=>r.LabelKind==null?null:TextResolver?.Invoke(r.LabelKind,r.LabelValue!);
    public string? ValueText(StatInfoRow r)=>r.ValueName==null?null:ValueProvider?.Invoke(r.ValueName);

    public void Paint(UiCanvas canvas){
        if(!open)return;
        canvas.DrawSurface(BaseImage,Origin.X,Origin.Y);
        foreach(var r in Rows){
            if(LabelText(r) is {Length:>0} label)Text(canvas,label,r.LabelOffset,r.LabelSize,"left",r.LabelVAlign,r.LabelColor);
            if(ValueText(r) is {Length:>0} value)Text(canvas,value,r.ValueOffset,r.ValueSize,r.ValueHAlign,r.ValueVAlign,r.ValueColor);
        }
    }
    void Text(UiCanvas canvas,string text,Point offset,Size box,string h,string v,Color color){
        var size=UiText.Measure(canvas.Graphics,text);
        int x=Origin.X+offset.X,y=Origin.Y+offset.Y;
        if(box.Width>0)x+=h switch{"center"=>(box.Width-size.Width)/2,"right"=>box.Width-size.Width,_=>0};
        if(box.Height>0)y+=v switch{"middle" or "center"=>(box.Height-size.Height)/2,"bottom"=>box.Height-size.Height,_=>0};
        canvas.DrawText(text,x,y,color);
    }

    public bool IsDragHandle(Point location)=>open&&Bounds.Contains(location)&&location.Y<Origin.Y+MoveableHeight;
    public void MoveTo(Point location)=>Origin=location;
    /// <summary>MouseEvent="Dummy": presses inside are swallowed; nothing is clickable.</summary>
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>open&&Bounds.Contains(e.Location);
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!open||!e.Down||e.Repeat||e.KeyCode!=Keys.Escape)return false;
        Close();return true;
    }
}
