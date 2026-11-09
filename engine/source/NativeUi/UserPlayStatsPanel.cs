namespace P06.NativeUi;

// R2-D（T254／#615）：死亡統計窗 UserPlayStatsUI 的原生面板（UserPlayUI.xml，WO§6；gap-matrix 第 4.1 節）。
// 已驗證（XML）：底 BaseSurf 5322、外框 FrameSurf 5323；重新開始 (66,40) 1953／1955／1954、結束 (66,61) 1956–1958、
//   取消 (66,82) 1959–1961、關閉 (215,15) 4429／4430；統計清單 (34,120)，四列的圖示、名稱（字串 3123–3126）與數值欄寬。
// 推測（標明）：外框疊在底圖上、兩者都畫在視窗原點；按鈕狀態 BaseSurf＝平常、HighlightSurf＝滑鼠停留、PushSurf＝按下；
//   Moveable="20"＝頂端 20 px 可拖曳；視窗畫面位置（XML 是 0,0）照 QUIT_WINDOW 的已驗證規則水平置中、y＝150；Esc＝取消。
// T272（#649，SS§3、FU3§4.2、SS2§14）：四列數值改由客戶端基準相減（UserPlayStatsValues，Recovery.cs）；沒有基準的列仍顯示「—」。
//   殺怪數恆為 0（3.81 不送 S250／61）。NewItemList 鈕（5325–5327）：滑過顯示 3131 提示；清單空時送系統訊息 3127；
//   地監時間清單 DungeonTime_List：S250／159 的名稱與分鐘（1587／3344）。
// T290（#698，SS§3.1）：清單不空時開關 UserObtainUI 清單窗（NativeUi/UserObtainPanel.cs，ObtainListOpen）；
//   Reset_Button（5330–5332，sub_C0BA30）：重建基準、清空新物品清單、關閉清單窗，之後停用 1000 ms；會送的 op 55／44 在 3.81 被忽略，不送。
// 推測（標明）：提示文字只畫字、不畫底框；地監清單超過 XML 列數的筆數不顯示，少於列數時剩下的列留空；還沒收到 159 時顯示 XML 預設字；
//   NewItemList 再按一次＝關掉清單窗（XML Toggle="true"）；冷卻中的 Reset 鈕只畫 BaseSurf。

public sealed record UserPlayStatsView(bool CanRestart,UserPlayStatsValues? Values=null);

/// <summary>UserPlayStatsUI 的資料與動作（由 Recovery.cs 的 ClientForm 接上既有死亡流程；面板不碰 LoginConnection）。</summary>
public interface IUserPlayStatsSource {
    /// <summary>null＝不開窗（活著、已取消或延後中）。</summary>
    UserPlayStatsView? Current {get;}
    void Restart();
    void Quit();
    /// <summary>取消／關閉／Esc：只關窗，計時照走（DeathWatch.Dismiss）。</summary>
    void Dismiss();
    /// <summary>系統訊息（NewItemList 清單空時的 3127）。</summary>
    void ShowMessage(string text){}
    /// <summary>T290 重設鈕：重建基準（LoginConnection.ResetUserPlayStats）。冷卻由面板管。</summary>
    void Reset(){}
}

public sealed class UserPlayStatsPanel:IUiMovablePanel {
    [UiPanel(150)] internal static UiPanelRegistration Registration=>new(UserPlayStatsPanel.PanelId,UserPlayStatsPanel.LayoutName,_=>new UserPlayStatsPanel());   // R2-D (#615): death window (UserPlayUI.xml)
    public const string PanelId=UserPlayStatsLayout.RootName;
    public const string LayoutName=UserPlayStatsLayout.FileName;
    /// <summary>推測：沿用 QUIT_WINDOW 已驗證的畫面位置（400 − 寬/2, 150）。</summary>
    public const int ScreenY=150;
    public const string ValuePlaceholder="—";

    public sealed record Button(UserPlayStatsButton Spec,UiSurface? Base,UiSurface? Highlight,UiSurface? Push) {
        public Rectangle Local=>Base==null?Rectangle.Empty:new(Spec.X,Spec.Y,Base.Width,Base.Height);
    }
    public sealed record Row(UserPlayStatsRow Spec,UiSurface? Icon,string Label,bool LabelFromTable);
    /// <summary>DungeonTime_List 的一列（與統計列同樣的 圖示 → 名稱 → 時間 排法）。Name／Time＝XML 預設字（G／S／L／空白、0）。</summary>
    public sealed record DungeonRow(UserPlayStatsRow Spec,UiSurface? Icon,string Name,string Time);
    /// <summary>Tooltip 相對於按鈕的位置與字串編號（XML Tooltip X/Y/String）。</summary>
    public sealed record Tooltip(int X,int Y,int StringId);

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds{get;private set;}
    public bool Visible=>view!=null;
    public IUserPlayStatsSource? Source{get;set;}
    public UserPlayStatsLayout? Layout{get;private set;}
    public UiSurface? BaseSurface{get;private set;}
    public UiSurface? FrameSurface{get;private set;}
    public Button? Restart{get;private set;}
    public Button? Quit{get;private set;}
    public Button? Cancel{get;private set;}
    public Button? Close{get;private set;}
    public Button? NewItemList{get;private set;}
    public Button? Reset{get;private set;}
    /// <summary>UserObtainUI 清單窗是否開著（UserObtainPanel 讀這個值決定 Visible）。統計窗關掉、重設時一併關。</summary>
    public bool ObtainListOpen{get;set;}
    /// <summary>重設鈕再次可用的時間（IUiGameState.Now 尺度）。</summary>
    public long ResetReadyAt{get;private set;}
    public bool ResetReady=>now>=ResetReadyAt;
    long now;
    public Tooltip? NewItemTooltip{get;private set;}
    public UiSurface? DungeonImage{get;private set;}
    public Point DungeonImageAt{get;private set;}
    public Point DungeonListAt{get;private set;}
    public IReadOnlyList<DungeonRow> DungeonRows{get;private set;}=[];
    public IReadOnlyList<Row> Rows{get;private set;}=[];
    public UserPlayStatsView? View=>view;
    /// <summary>滑鼠停留／按下中的按鈕（狀態圖用）。</summary>
    public Button? Hover{get;private set;}
    public Button? Pressed{get;private set;}
    UserPlayStatsView? view;Size size;bool placed;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var l=UserPlayStatsLayout.From(layout);Layout=l;
        UiSurface? S(int? id)=>id is int n?surf(n):null;
        BaseSurface=S(l.BaseSurf);FrameSurface=S(l.FrameSurf);
        Button B(UserPlayStatsButton spec)=>new(spec,S(spec.BaseSurf),S(spec.HighlightSurf),S(spec.PushSurf));
        Restart=B(l.Restart);Quit=B(l.Quit);Cancel=B(l.Cancel);Close=l.Close is {} c?B(c):null;Reset=l.Reset is {} rs?B(rs):null;
        Rows=l.Rows.Select(r=>{var (text,table)=UserPlayStatsText.Label(r.StringId,r.Name);return new Row(r,S(r.IconSurf),text,table);}).ToArray();
        LoadExtras(layout,l,S);
        var rects=new List<Rectangle>();
        if(BaseSurface!=null)rects.Add(new(0,0,BaseSurface.Width,BaseSurface.Height));
        if(FrameSurface!=null)rects.Add(new(0,0,FrameSurface.Width,FrameSurface.Height));
        rects.AddRange(Buttons.Select(b=>b.Local).Where(r=>!r.IsEmpty));
        var union=rects.Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        size=union.IsEmpty?Size.Empty:new(union.Right,union.Bottom);
        Place(new(UiRoot.LogicalWidth/2-size.Width/2+l.WindowX,ScreenY+l.WindowY));placed=false;
    }
    IEnumerable<Button> Buttons=>new[]{Restart,Quit,Cancel,Close,NewItemList,Reset}.OfType<Button>();
    // T272：NewItemList 鈕（在 3126 列的數值欄之後，橫向清單同一個 Gap）與地監時間清單。都是選用節點：缺了就不畫。
    void LoadExtras(UiLayout layout,UserPlayStatsLayout l,Func<int?,UiSurface?> S){
        NewItemList=null;NewItemTooltip=null;DungeonImage=null;DungeonRows=[];
        var window=layout.Root.Child("Window");
        var statsList=window.ChildrenNamed("List").SingleOrDefault(n=>n.Attr("Name")=="UserPlayStats_List");
        var rowNodes=statsList?.ChildrenNamed("List").ToList()??[];
        for(int i=0;i<rowNodes.Count&&i<l.Rows.Count;i++){
            var node=rowNodes[i].ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="NewItemList_Button");if(node==null)continue;
            var r=l.Rows[i];int x=l.ListX;if(S(r.IconSurf) is {} icon)x+=icon.Width+r.Gap;
            x+=r.LabelWidth+r.Gap+r.ValueWidth+r.Gap;int y=l.ListY+i*l.RowHeight+(node.TryInt("Y")??0);
            var spec=new UserPlayStatsButton("NewItemList_Button",x,y,UserPlayStatsLayout.FirstSurf(node,"BaseSurf"),node.RequiredAttr("MouseEvent"),UserPlayStatsLayout.FirstSurf(node,"HighlightSurf"),UserPlayStatsLayout.FirstSurf(node,"PushSurf"));
            NewItemList=new(spec,S(spec.BaseSurf),S(spec.HighlightSurf),S(spec.PushSurf));
            if(node.ChildrenNamed("Tooltip").FirstOrDefault() is {} tip&&tip.TryInt("String") is int sid)NewItemTooltip=new(tip.TryInt("X")??0,tip.TryInt("Y")??0,sid);
            break;
        }
        if(window.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="DungeonTime_Image") is {} image){DungeonImage=S(UserPlayStatsLayout.FirstSurf(image,"Surf"));DungeonImageAt=new(image.TryInt("X")??0,image.TryInt("Y")??0);}
        if(window.ChildrenNamed("List").SingleOrDefault(n=>n.Attr("Name")=="DungeonTime_List") is {} dungeon){
            DungeonListAt=new(dungeon.Int("X"),dungeon.Int("Y"));
            DungeonRows=dungeon.ChildrenNamed("List").Select(row=>{
                var labels=row.ChildrenNamed("Label").ToList();
                if(labels.Count<2)throw new UiLayoutException(layout.FileName,row.Path,"地監時間列需要名稱與時間兩個 Label");
                var icon=row.ChildrenNamed("Image").FirstOrDefault();int? surf=icon==null?null:UserPlayStatsLayout.FirstSurf(icon,"Surf");
                var spec=new UserPlayStatsRow(row.RequiredAttr("Name"),surf,icon?.TryInt("Y")??0,null,labels[0].Int("Width"),labels[1].Int("Width"),row.Int("Height"),row.TryInt("Gap")??0,FontColor(labels[0]),FontColor(labels[1]));
                return new DungeonRow(spec,S(surf),labels[0].Attr("Caption")??"",labels[1].Attr("Caption")??"");
            }).ToArray();
        }
    }
    static Color FontColor(UiNode node){
        string? raw=node.Attr("FontColor");if(raw==null)return Color.White;
        var parts=raw.Split(',');
        if(parts.Length!=3||parts.Any(p=>!byte.TryParse(p.Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out _)))throw new UiLayoutException(node.Layout.FileName,node.Path,$"FontColor=\"{raw}\" 不是 R,G,B");
        byte P(int i)=>byte.Parse(parts[i].Trim(),System.Globalization.CultureInfo.InvariantCulture);
        return Color.FromArgb(P(0),P(1),P(2));
    }
    /// <summary>地監時間第 i 列在畫布上的位置（同 <see cref="RowRects"/> 的排法）。</summary>
    public (Point Icon,Rectangle Name,Rectangle Time) DungeonRowRects(int index){
        var r=DungeonRows[index];int x=Origin.X+DungeonListAt.X,y=Origin.Y+DungeonListAt.Y+index*r.Spec.Height;
        var icon=new Point(x,y+r.Spec.IconY);if(r.Icon is {} s)x+=s.Width+r.Spec.Gap;
        var name=new Rectangle(x,y,r.Spec.LabelWidth,r.Spec.Height);
        return (icon,name,new Rectangle(name.Right+r.Spec.Gap,y,r.Spec.ValueWidth,r.Spec.Height));
    }
    /// <summary>地監時間第 i 列的文字：還沒收到 S250／159→XML 預設字；有清單→第 i 筆（超過筆數留空）。</summary>
    public (string Name,string Time) DungeonText(int index){
        var times=view?.Values?.DungeonTimes;
        if(times==null)return (DungeonRows[index].Name,DungeonRows[index].Time);
        return index<times.Count?(times[index].MapName,UserPlayStatsText.DungeonTime(times[index].Minutes)):("","");
    }
    /// <summary>統計列 i 顯示的數值；沒有基準時是「—」。</summary>
    public string ValueText(int index)=>UserPlayStatsText.Value(view?.Values,Rows[index].Spec.StringId)??ValuePlaceholder;
    void Place(Point origin){Origin=origin;Bounds=size.IsEmpty?Rectangle.Empty:new(origin,size);}

    public bool IsDragHandle(Point location)=>Layout?.Moveable is int h&&h>0&&Bounds.Contains(location)&&location.Y<Origin.Y+h&&ButtonAt(location)==null;
    public void MoveTo(Point location){Place(location);placed=true;}
    public bool Moved=>placed;

    public void Update(IUiGameState state){
        var v=Source?.Current;now=state.Now;
        if(v==null){Hover=null;Pressed=null;ObtainListOpen=false;}
        view=v;
    }

    Rectangle At(Button b)=>b.Local.IsEmpty?Rectangle.Empty:new(Origin.X+b.Local.X,Origin.Y+b.Local.Y,b.Local.Width,b.Local.Height);
    public Button? ButtonAt(Point p)=>Buttons.FirstOrDefault(b=>At(b).Contains(p));
    /// <summary>狀態圖（推測，見檔頭）：按下中且滑鼠在鈕上→PushSurf；停留→HighlightSurf；缺圖退回 BaseSurf。重新開始不可用時只畫 BaseSurf。</summary>
    public UiSurface? StateSurface(Button b){
        if(b==Restart&&view is {CanRestart:false})return b.Base;
        if(b==Reset&&!ResetReady)return b.Base;
        if(Pressed==b&&Hover==b)return b.Push??b.Base;
        if(Hover==b)return b.Highlight??b.Base;
        return b.Base;
    }
    /// <summary>統計列在畫布上的位置（清單 X/Y＋列高；列內 圖示 → 間距 → 名稱 → 間距 → 數值，推測）。</summary>
    public (Point Icon,Rectangle Label,Rectangle Value) RowRects(int index){
        var l=Layout!;var r=Rows[index].Spec;int x=Origin.X+l.ListX,y=Origin.Y+l.ListY+index*l.RowHeight;
        var icon=new Point(x,y+r.IconY);
        if(Rows[index].Icon is {} s)x+=s.Width+r.Gap;
        var label=new Rectangle(x,y,r.LabelWidth,r.Height);
        return (icon,label,new Rectangle(label.Right+r.Gap,y,r.ValueWidth,r.Height));
    }

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        if(BaseSurface!=null)canvas.DrawSurface(BaseSurface,Origin.X,Origin.Y);
        if(FrameSurface!=null)canvas.DrawSurface(FrameSurface,Origin.X,Origin.Y);
        foreach(var b in Buttons)if(StateSurface(b) is {} s)canvas.DrawSurface(s,Origin.X+b.Spec.X,Origin.Y+b.Spec.Y);
        for(int i=0;i<Rows.Count;i++){
            var row=Rows[i];var (icon,label,value)=RowRects(i);
            if(row.Icon!=null)canvas.DrawSurface(row.Icon,icon.X,icon.Y);
            canvas.DrawText(row.Label,label.X,label.Y,row.Spec.LabelColor);
            string text=ValueText(i);int w=UiText.Measure(canvas.Graphics,text).Width;
            canvas.DrawText(text,value.Right-w,value.Y,row.Spec.ValueColor);
        }
        if(DungeonImage!=null)canvas.DrawSurface(DungeonImage,Origin.X+DungeonImageAt.X,Origin.Y+DungeonImageAt.Y);
        for(int i=0;i<DungeonRows.Count;i++){
            var row=DungeonRows[i];var (icon,name,time)=DungeonRowRects(i);var (n,tm)=DungeonText(i);
            if(row.Icon!=null)canvas.DrawSurface(row.Icon,icon.X,icon.Y);
            if(n.Length>0)canvas.DrawText(n,name.X,name.Y,row.Spec.LabelColor);
            if(tm.Length>0){int w=UiText.Measure(canvas.Graphics,tm).Width;canvas.DrawText(tm,time.Right-w,time.Y,row.Spec.ValueColor);}
        }
        if(Hover==NewItemList&&NewItemList!=null&&NewItemTooltip is {} tip)
            canvas.DrawText(UserPlayStatsText.Text(tip.StringId),Origin.X+NewItemList.Spec.X+tip.X,Origin.Y+NewItemList.Spec.Y+tip.Y,Color.White,Color.Black);
    }

    void Fire(Button b){
        if(Source==null)return;
        if(b==Restart){if(view is {CanRestart:true})Source.Restart();}
        else if(b==Quit)Source.Quit();
        // sub_C0B470：新物品清單空時只顯示 3127；不空時開關 UserObtainUI 清單窗。
        else if(b==NewItemList){
            if(view?.Values is not {NewItems:>0}){ObtainListOpen=false;Source.ShowMessage(UserPlayStatsText.Text(UserPlayStatsText.NoItemsMessage));}
            else ObtainListOpen=!ObtainListOpen;
        }
        // sub_C0BA30：只在可用時有效；重建基準、關清單窗，停用 1000 ms。
        else if(b==Reset){
            if(!ResetReady)return;
            Source.Reset();ObtainListOpen=false;ResetReadyAt=now+UserPlayStatsRules.ResetCooldownMs;
        }
        else Source.Dismiss();
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        var hit=ButtonAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:Hover=hit;return Bounds.Contains(e.Location)||Pressed!=null;
            case UiMouseKind.Down:
                if(e.Button!=MouseButtons.Left)return Bounds.Contains(e.Location);
                Hover=hit;Pressed=hit;return Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                var pressed=Pressed;Pressed=null;Hover=hit;
                if(pressed!=null&&pressed==hit)Fire(pressed);
                return true;
            }
            default:return Bounds.Contains(e.Location);
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down||e.KeyCode!=Keys.Escape)return false;
        Source?.Dismiss();return true;
    }
}
