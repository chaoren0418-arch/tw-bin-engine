namespace P06.NativeUi;

// M7-W（T223／#530）ui-summon：CSummonUI（SummonUI.xml＋summonList.xml，docs/reference/windows.md §1.3）。
// 版面全部在執行期從 Tile.pak 讀（不複製遊戲資源）；元件位置＝XML 座標（相對視窗），清單依 XML 的 Horizontal／Vertical／Gap
// 與元件底圖尺寸排列。缺 surf 的元件不畫（規則 5）；視窗底圖缺少時整個面板停用並記一行日誌。
// 已驗證（XML／協定）：快速 3 格、12 個等級鈕、每級最多 6 隻（2×3）、魅力標籤、確定／取消／返回、31 隻 action 與 surf。
// 已驗證（真實 surf 渲染，#530 報告）：等級鈕底圖本身寫著「等級 72 以上」…「等級 28 以上」，列優先由高到低。
// 推測（報告已列）：怪物圖示畫在格子左上、選取框（OverSurf）畫在怪物圖之下；Back 取代 Cancel；文字白色；初始位置用 XML 的 (X,Y)。
// 待驗證（不做）：快速 3 格的內容（只畫 XML 預設的 "?"，不可點）、Desc tooltip（字串表）、DisableOverSurf 的時機、ESC 行為。

public sealed record SummonMonster(int Id,int Level,string Action,int Desc,int Surf,int MinCha);

/// <summary>summonList.xml：&lt;summonlist&gt;&lt;summon level&gt;&lt;monster id level action desc surf minCha/&gt;…</summary>
public static class SummonCatalog {
    public const string FileName="summonList";
    public static IReadOnlyList<SummonMonster> Parse(UiLayout list){
        if(list.Root.Name!="summonlist")throw new UiLayoutException(list.FileName,list.Root.Path,"根元素不是 summonlist");
        var rows=new List<SummonMonster>();var ids=new HashSet<int>();var actions=new HashSet<string>(StringComparer.Ordinal);var levels=new HashSet<int>();
        foreach(var group in list.Root.ChildrenNamed("summon")){
            int level=group.Int("level");
            if(level<=0||!levels.Add(level))throw new UiLayoutException(list.FileName,group.Path,$"summon level {level} 不合法或重複");
            foreach(var m in group.ChildrenNamed("monster")){
                string action=m.RequiredAttr("action").Trim();
                var row=new SummonMonster(m.Int("id"),m.Int("level"),action,m.Int("desc"),m.Int("surf"),m.Int("minCha"));
                if(row.Level!=level)throw new UiLayoutException(list.FileName,m.Path,"monster level 與所屬 summon level 不同");
                if(!P06.PetPackets.ValidSummonAction(action))throw new UiLayoutException(list.FileName,m.Path,$"action=\"{action}\" 不是召喚編號");
                if(!ids.Add(row.Id)||!actions.Add(action))throw new UiLayoutException(list.FileName,m.Path,"monster id 或 action 重複");
                if(row.Surf<0||row.MinCha<0)throw new UiLayoutException(list.FileName,m.Path,"surf／minCha 為負數");
                rows.Add(row);
            }
        }
        if(rows.Count==0)throw new UiLayoutException(list.FileName,list.Root.Path,"沒有任何 monster");
        return rows;
    }
    public static int[] Levels(IReadOnlyList<SummonMonster> rows)=>rows.Select(r=>r.Level).Distinct().Order().ToArray();
    /// <summary>
    /// T276（#667）召喚控制戒指入口：S36 type 5＋施放技能 51 時，AbilityStatus.cs 的 TryOpenRingWindow 開本機請求（Source＝"local"），
    /// 本面板照常顯示。送出前的等級檢查（sub_C00890 FO 0x7FFC90／sub_C00F50 FO 0x800350）：
    /// (等級 − 28)/4 + 7 &lt; action % 256 時顯示 743「等級太低而無法召喚怪物」、不送。整數除法向零取整（同 x86 idiv）。
    /// </summary>
    public static bool RingLevelAllows(int level,string action)=>
        P06.PetPackets.ValidSummonAction(action)&&uint.TryParse(action,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out uint v)&&(level-28)/4+7>=(int)(v%256);
}

/// <summary>面板讀的狀態（由 PetUi.cs 的轉接提供）。Level／Charisma 是原服 S8 的值，沒送就是 null（顯示空白）。</summary>
public sealed record SummonUiState(bool Open,long Revision,string Source,int? Level,int? Charisma,bool CanSend);
public interface ISummonUiHost {
    SummonUiState State{get;}
    /// <summary>確定鈕：對 shown 這一次的請求送出 monster；不合法或已過期時由宿主拒絕。</summary>
    void Choose(SummonUiState shown,SummonMonster monster);
    void Close(SummonUiState shown);
}

public sealed class SummonPanel:IUiMovablePanel,IUiClosablePanel {
    [UiPanel(30)] internal static UiPanelRegistration Registration=>new(SummonPanel.PanelId,SummonPanel.LayoutName,root=>new SummonPanel(root.Resources));   // ui-summon (M7-W, #530)
    public const string PanelId="SummonMonster_Window",LayoutName="SummonUI";
    public const int QuickSlots=3,LevelButtons=12,MonsterSlots=6;
    internal sealed record Part(string Key,Rectangle Rect,int Base,int? Push);
    internal sealed record Slot(Part Part,Point LabelOrigin,Size LabelSize,string Caption,Point LabelImageAt,int LabelImage,int Over);

    readonly UiResources? resources;
    Func<int,UiSurface?> surf=_=>null;
    IReadOnlyList<SummonMonster> monsters=[];int[] levels=[];
    Point origin;Size size;int moveable;int baseSurf,frameSurf;bool usable;
    Part close=null!,ok=null!,cancel=null!,back=null!;
    (Point At,int Surf) title,charismaText,charismaImage;Rectangle charismaLabel;
    Slot[] quick=[],monsterSlots=[];Part[] levelParts=[];
    SummonUiState? shown;long dismissed=-1;int? selectedLevel;SummonMonster? selected;string? pressed;

    public SummonPanel(UiResources? resources=null){this.resources=resources;}
    public ISummonUiHost? Host{get;private set;}
    public void Attach(ISummonUiHost host)=>Host=host;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>new(origin,size);
    public bool Visible=>usable&&shown is {Open:true} s&&s.Revision!=dismissed;
    internal bool Usable=>usable;
    internal int? SelectedLevel=>selectedLevel is int i?LevelOf(i):null;
    /// <summary>等級鈕 i（列優先）→ 等級：第 0 鈕是最高級（底圖文字「等級 72 以上」），最後一鈕是最低級。</summary>
    int LevelOf(int button)=>levels[levels.Length-1-button];
    internal SummonMonster? Selected=>selected;
    internal IReadOnlyList<SummonMonster> Monsters=>monsters;

    public void Load(UiLayout layout,SurfCatalog surfs){
        var list=(resources??throw new UiLayoutException(layout.FileName,"","沒有客戶端資源，無法讀取 summonList.xml")).Xml.Load(SummonCatalog.FileName);
        Load(layout,list,surfs.Get);
    }
    /// <summary>測試與正式共用：layout＝SummonUI.xml、list＝summonList.xml、surfs＝surf id → 圖（可為 null）。</summary>
    internal void Load(UiLayout layout,UiLayout list,Func<int,UiSurface?> surfs){
        surf=surfs;monsters=SummonCatalog.Parse(list);levels=SummonCatalog.Levels(monsters);
        var window=Direct(layout.Root,"Window","SummonMonster_Window");
        origin=new(window.Int("X"),window.Int("Y"));moveable=window.Int("Moveable");
        baseSurf=SurfId(window,"BaseSurf");frameSurf=SurfId(window,"FrameSurf");
        var missing=new List<string>();
        Size? Measure(int id){var s=surf(id);return s==null?null:new Size(s.Width,s.Height);}
        Part Button(UiNode n,Point at,string key){
            int b=SurfId(n,"BaseSurf");int? push=n.Attr("PushSurf")!=null?SurfId(n,"PushSurf"):null;
            var s=Measure(b);if(s==null)missing.Add($"{n.Path} BaseSurf {b}");
            return new(key,new(at,s??Size.Empty),b,push);
        }
        Point Pos(UiNode n,Point parent)=>new(parent.X+n.Int("X"),parent.Y+n.Int("Y"));
        var windowSize=Measure(baseSurf);
        if(windowSize==null)missing.Add($"{window.Path} BaseSurf {baseSurf}");
        size=windowSize??Size.Empty;
        var zero=Point.Empty;
        close=Button(Direct(window,"Button","CloseWindow_Button"),Pos(Direct(window,"Button","CloseWindow_Button"),zero),"close");
        ok=Button(Direct(window,"Button","Ok_Button"),Pos(Direct(window,"Button","Ok_Button"),zero),"ok");
        cancel=Button(Direct(window,"Button","Cancel_Button"),Pos(Direct(window,"Button","Cancel_Button"),zero),"cancel");
        back=Button(Direct(window,"Button","Back_Button"),Pos(Direct(window,"Button","Back_Button"),zero),"back");
        (Point,int) Image(string name){var n=Direct(window,"Image",name);return (Pos(n,zero),SurfId(n,"Surf"));}
        title=Image("SummonTitle_Image");charismaText=Image("CharismaText_Image");charismaImage=Image("CharismaLabel_Image");
        var cha=Direct(window,"Label","Charisma_Label");charismaLabel=new(Pos(cha,zero),new(cha.Int("Width"),cha.Int("Height")));

        Slot ToSlot(UiNode item,Rectangle rect,string key){
            var label=Direct(item,"Label","Level_Label");var image=Direct(item,"Image","LevelLabel_Image");
            return new(new(key,rect,SurfId(item,"BaseSurf"),null),new(label.Int("X"),label.Int("Y")),new(label.Int("Width"),label.Int("Height")),
                label.RequiredAttr("Caption"),new(image.Int("X"),image.Int("Y")),SurfId(image,"Surf"),SurfId(item,"OverSurf"));
        }
        var quickList=Direct(window,"List","QuickSummonLevel_List");
        quick=Flow(quickList,Pos(quickList,zero),Measure,missing).Select((x,i)=>ToSlot(x.Node,x.Rect,"quick:"+i)).ToArray();
        var levelList=Direct(window,"List","Level_List");
        levelParts=Flow(levelList,Pos(levelList,zero),Measure,missing).Select((x,i)=>Button(x.Node,x.Rect.Location,"level:"+i)).ToArray();
        var control=Direct(window,"Control","SummonLevelList_Control");var inner=Direct(control,"List","SummonLevel_List");
        monsterSlots=Flow(inner,Pos(inner,Pos(control,zero)),Measure,missing).Select((x,i)=>ToSlot(x.Node,x.Rect,"monster:"+i)).ToArray();
        if(quick.Length!=QuickSlots)throw new UiLayoutException(layout.FileName,quickList.Path,$"快速召喚格數 {quick.Length}，應為 {QuickSlots}");
        if(levelParts.Length!=LevelButtons||levels.Length!=LevelButtons)throw new UiLayoutException(layout.FileName,levelList.Path,$"等級鈕 {levelParts.Length} 個、summonList 等級 {levels.Length} 個，應各為 {LevelButtons}");
        if(monsterSlots.Length!=MonsterSlots)throw new UiLayoutException(layout.FileName,inner.Path,$"怪物格數 {monsterSlots.Length}，應為 {MonsterSlots}");
        if(levels.Any(l=>monsters.Count(m=>m.Level==l)>MonsterSlots))throw new UiLayoutException(list.FileName,list.Root.Path,$"同一等級超過 {MonsterSlots} 隻");
        usable=missing.Count==0;
        if(!usable)UiResourceLog.Write($"panel {PanelId} 停用：缺少版面所需的 surf（{string.Join("；",missing)}）");
    }

    static UiNode Direct(UiNode parent,string element,string name){
        var found=parent.ChildrenNamed(element).Where(c=>c.Attr("Name")==name).Take(2).ToList();
        if(found.Count!=1)throw new UiLayoutException(parent.Layout.FileName,parent.Path,$"{element} Name=\"{name}\" {(found.Count==0?"缺少":"不唯一")}");
        return found[0];
    }
    /// <summary>"4893;1" → 4893（';' 後的欄位意義待查證，只取 surf id）。</summary>
    internal static int SurfId(UiNode n,string attr){
        string raw=n.RequiredAttr(attr).Trim();string head=raw.Split(';')[0].Trim();
        if(!int.TryParse(head,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int v))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是 surf id");
        return v;
    }
    /// <summary>List 排列：Horizontal="1" 或 Vertical="1"（擇一）＋Gap；子 List 用 Width／Height，Button／ListItem 用 BaseSurf 尺寸。回傳葉節點。</summary>
    static List<(UiNode Node,Rectangle Rect)> Flow(UiNode list,Point at,Func<int,Size?> measure,List<string> missing){
        bool h=list.Attr("Horizontal")=="1",v=list.Attr("Vertical")=="1";
        if(h==v)throw new UiLayoutException(list.Layout.FileName,list.Path,"List 必須是 Horizontal 或 Vertical 其中之一");
        int gap=list.Int("Gap");var result=new List<(UiNode,Rectangle)>();int x=at.X,y=at.Y;
        foreach(var child in list.Children){
            Size s;
            if(child.Name=="List"){s=new(child.Int("Width"),child.Int("Height"));result.AddRange(Flow(child,new(x,y),measure,missing));}
            else if(child.Name is "ListItem" or "Button"){
                int b=SurfId(child,"BaseSurf");var m=measure(b);
                if(m==null){missing.Add($"{child.Path} BaseSurf {b}");m=Size.Empty;}
                s=m.Value;result.Add((child,new(new(x,y),s)));
            }
            else continue;
            if(h)x+=s.Width+gap;else y+=s.Height+gap;
        }
        return result;
    }

    public void Update(IUiGameState state){
        var now=Host?.State;
        if(now==null||!state.InWorld){shown=null;Clear();return;}
        if(shown?.Revision!=now.Revision)Clear();
        shown=now;
    }
    void Clear(){selectedLevel=null;selected=null;pressed=null;}

    IEnumerable<SummonMonster> LevelMonsters=>selectedLevel is int i?monsters.Where(m=>m.Level==LevelOf(i)):[];
    bool Enabled(SummonMonster m)=>shown?.Level is int lv&&lv>=m.Level;
    /// <summary>目前可點的元件（相對視窗座標）。</summary>
    internal IEnumerable<Part> Active(){
        yield return close;
        if(selectedLevel==null){foreach(var p in levelParts)yield return p;yield return cancel;}
        else{
            int k=0;foreach(var m in LevelMonsters){if(Enabled(m))yield return monsterSlots[k].Part;k++;}
            yield return back;
        }
        yield return ok;
    }
    internal string? HitKey(Point logical){
        var p=new Point(logical.X-origin.X,logical.Y-origin.Y);
        return Active().FirstOrDefault(x=>x.Rect.Contains(p))?.Key;
    }
    internal Rectangle Absolute(Rectangle r)=>new(r.X+origin.X,r.Y+origin.Y,r.Width,r.Height);
    internal Rectangle PartRect(string key)=>Absolute(new[]{close,ok,cancel,back}.Concat(levelParts).Concat(quick.Select(q=>q.Part)).Concat(monsterSlots.Select(s=>s.Part)).First(p=>p.Key==key).Rect);
    bool OkEnabled=>selected!=null&&shown is {CanSend:true};

    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        void Draw(int id,Point at)=>canvas.DrawSurface(surf(id),origin.X+at.X,origin.Y+at.Y);
        void Text(string text,Point at,Size box){
            if(text.Length==0)return;
            var m=UiText.Measure(canvas.Graphics,text);
            canvas.DrawText(text,origin.X+at.X+(box.Width-m.Width)/2,origin.Y+at.Y+(box.Height-m.Height)/2,Color.White);
        }
        void Button(Part p,bool enabled=true)=>Draw(enabled&&pressed==p.Key&&p.Push is int push?push:p.Base,p.Rect.Location);
        void DrawSlot(Slot s,int? monsterSurf,string caption,bool highlight){
            Draw(s.Part.Base,s.Part.Rect.Location);
            if(highlight)Draw(s.Over,s.Part.Rect.Location);
            if(monsterSurf is int ms)Draw(ms,s.Part.Rect.Location);
            var at=s.Part.Rect.Location;
            Draw(s.LabelImage,new(at.X+s.LabelImageAt.X,at.Y+s.LabelImageAt.Y));
            Text(caption,new(at.X+s.LabelOrigin.X,at.Y+s.LabelOrigin.Y),s.LabelSize);
        }
        Draw(baseSurf,Point.Empty);Draw(frameSurf,Point.Empty);
        Button(close);
        Draw(title.Surf,title.At);Draw(charismaText.Surf,charismaText.At);Draw(charismaImage.Surf,charismaImage.At);
        Text(shown?.Charisma?.ToString()??"",charismaLabel.Location,charismaLabel.Size);
        foreach(var q in quick)DrawSlot(q,null,q.Caption,false);
        if(selectedLevel==null){foreach(var p in levelParts)Button(p);Button(cancel);}
        else{
            int k=0;
            foreach(var m in LevelMonsters){DrawSlot(monsterSlots[k],m.Surf,m.Level.ToString(),selected==m);k++;}
            Button(back);
        }
        Button(ok,OkEnabled);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(e.Button!=MouseButtons.Left)return Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Down){pressed=HitKey(e.Location);return true;}
        if(e.Kind==UiMouseKind.Up){
            string? key=HitKey(e.Location),was=pressed;pressed=null;
            if(key!=null&&key==was)Activate(key);
            return true;
        }
        return Bounds.Contains(e.Location);
    }
    /// <summary>
    /// R7-H（#810）：堆疊關窗（被擠出、全域 ESC；sub_A57C60 的 vtable+36）＝關閉鈕：記下這次請求、請宿主關掉。
    /// 已關著（沒有請求或已關過這一次）時不呼叫宿主。ESC 本身由全域 ESC 處理（本面板的 Key 仍不吃鍵，CSummonUI 的 ESC 細節見 Q7-3）。
    /// </summary>
    public void Close(){
        if(shown is not {Open:true} s||s.Revision==dismissed)return;
        dismissed=s.Revision;Clear();Host?.Close(s);
    }
    void Activate(string key){
        if(shown is not {} s)return;
        if(key is "close" or "cancel"){Close();return;}
        if(key=="back"){Clear();return;}
        if(key.StartsWith("level:",StringComparison.Ordinal)){selectedLevel=int.Parse(key[6..]);selected=null;return;}
        if(key.StartsWith("monster:",StringComparison.Ordinal)){var m=LevelMonsters.ElementAtOrDefault(int.Parse(key[8..]));if(m!=null&&Enabled(m))selected=m;return;}
        // 送出成功時原服請求的 revision 會改變而關窗；失敗則保留選擇，可再按一次（宿主自己擋重複送出）。
        if(key=="ok"&&OkEnabled)Host?.Choose(s,selected!);
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;

    public bool IsDragHandle(Point location)=>Visible&&Bounds.Contains(location)&&location.Y-origin.Y<moveable&&HitKey(location)==null;
    public void MoveTo(Point location)=>origin=location;
}
