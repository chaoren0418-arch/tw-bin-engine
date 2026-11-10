namespace P06.NativeUi;

// R9-B（T362／#846）：地圖窗右鍵選單 MapCommunicationSelectWindow 與聊天列上的 1785 提示（docs/reference/followups-9.md §7.2）。
// 已驗證（IDA，TW16 IDB SHA-256 3a0bf206…7e82f，見 docs/reports/issue-846.md）：
// - 建立 sub_A62870（VA 0xA62870／FO 0x661C70）：鈕 0 surf 2944（按下 2946）在 (0,0)；鈕 1 surf 2947（按下 2949）在 (0, surf 128 的高)；
//   回呼 sub_A762A0(0／1)。
// - 顯示 sub_A66950（vtable+32）：位置＝(min(滑鼠 x,560)+1, min(滑鼠 y,440)+1)，清掉 +9 旗標（發送提示中）。
// - 隱藏 sub_A68770（vtable+36）：+9 旗標開著時清掉並呼叫 sub_7DCE00（推測：收掉聊天列提示）。
// - 繪製 sub_A6AA10：選單開著時畫兩顆鈕；+9 旗標開著時另畫 surf 150 在 (選單 x−4, 選單 y−4)（推測：發送對象的點選標記）。
//   FU9 §7.2 寫「選單開著時另畫 150」需更正：是「發送提示進行中」才畫，選單那時已收起。
// - sub_A762A0：先收起選單；鈕 1 → MapLocationSaveWindow 顯示（vtable+32）；鈕 0 → sub_AC98E0(1785) 聊天列提示、+9 旗標＝1；
//   提示完成回呼 sub_A770F0 清掉旗標。
// - 選單的滑鼠（vtable+44 sub_A6E660）只交給子鈕；沒按到鈕的左／右鍵按下由 Field_Map（sub_A6F0B0）收起選單。
// 引擎做法：
// - 選單在 Float 層，Field_Map 透過 IFieldMapPopup 叫出／收起（本面板 Update 時把自己裝到 Field_Map，不改註冊表）。
// - 鈕 0 走既有 MapSendEntry（LocationListPanel 裝到 Field_Map 的 TransferHandler）：聊天輸入列打開並填「/loc 」；
//   1785 由 MapSendPromptPanel 畫在聊天輸入列上（輸入文字右側；確切 x 推測），不再畫在座標清單旁。
// - 缺圖就不畫：兩顆鈕的 surf 都沒有時選單不開。
// - 原生 UI 關閉（--classic-ui）時沒有這個選單，發送入口仍是聊天指令 /loc（WinForms）。

/// <summary>What Field_Map asks of its right-click menu (installed at run time by MapCommunicationPanel).</summary>
public interface IFieldMapPopup {
    bool MenuOpen{get;}
    /// <summary>sub_A66950 at the cursor. mapPoint = map coordinate under the cursor (sub_A66F50), null when no png is shown.</summary>
    bool ShowMenu(Point cursor,Point? mapPoint,IUiCommands commands);
    /// <summary>sub_A68770 (menu only; a send prompt that is already running is left to the chat line).</summary>
    void HideMenu();
    /// <summary>Top-left of surf 150 while the send prompt started from the menu is active; null otherwise.</summary>
    Point? Marker{get;}
}

/// <summary>Map coordinate handed from the menu to MapLocationSaveWindow (map id of the current snapshot).</summary>
public sealed record MapLocationTarget(int MapId,int X,int Y);

public sealed class MapCommunicationPanel(Func<string,IUiPanel?>? find=null):IUiClosablePanel,IFieldMapPopup {
    public const string PanelId="MapCommunicationSelectWindow";
    public const int SendSurf=2944,SendDownSurf=2946,SaveSurf=2947,SaveDownSurf=2949,SpacingSurf=128,MarkerSurf=150;
    /// <summary>sub_A66950 clamps the cursor to 560 × 440 before adding 1.</summary>
    public const int MaxX=560,MaxY=440,MarkerOffset=4;
    public const int SendButton=0,SaveButton=1;
    // T270（#645）自動發現：註冊宣告在面板旁，不改 UiPanelRegistry.cs。
    [UiPanel(192)] internal static UiPanelRegistration Registration=>new(PanelId,"uistatus86",root=>new MapCommunicationPanel(root.Find));   // R9-B (#846): Field_Map right-click menu, layout hard-coded in the BIN (sub_A62870)

    Func<int,UiSurface?>? surfs;
    bool inWorld,open,prompting;Point at;int? pressed;
    MapLocationTarget? target;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    public bool Visible=>inWorld&&open;
    public bool MenuOpen=>open;
    /// <summary>+9 flag: the 1785 prompt started from button 0 is running.</summary>
    public bool Prompting=>prompting;
    /// <summary>Menu top-left (logical).</summary>
    public Point Location=>at;
    /// <summary>Point the menu was opened on (map coordinate under the cursor, or the player's position without a png).</summary>
    public MapLocationTarget? Target=>target;
    public Point? Marker=>prompting?new Point(at.X-MarkerOffset,at.Y-MarkerOffset):null;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    /// <summary>Same as Load(layout, catalog) with any surface source (offline checks use in-memory surfaces).</summary>
    public void Load(Func<int,UiSurface?> surfs)=>this.surfs=surfs;
    UiSurface? Surf(int id)=>surfs?.Invoke(id);

    /// <summary>
    /// Button rectangle (logical); empty when its surf is missing (not drawn, cannot be hit). Button 1 sits at y = height of
    /// surf 128, so without 128 it has no position and is left out too (never a guessed offset).
    /// </summary>
    public Rectangle ButtonRect(int button){
        if(Surf(button==SendButton?SendSurf:SaveSurf) is not {} s)return Rectangle.Empty;
        if(button==SendButton)return new(at.X,at.Y,s.Width,s.Height);
        return Surf(SpacingSurf) is {} spacing?new(at.X,at.Y+spacing.Height,s.Width,s.Height):Rectangle.Empty;
    }
    public Rectangle Bounds{get{
        if(!open)return Rectangle.Empty;
        var a=ButtonRect(SendButton);var b=ButtonRect(SaveButton);
        return a.IsEmpty?b:b.IsEmpty?a:Rectangle.Union(a,b);
    }}

    FieldMapPanel? Field=>find?.Invoke(FieldMapPanel.PanelId) as FieldMapPanel;
    MapSendEntry? Entry=>(find?.Invoke(LocationListPanel.PanelId) as LocationListPanel)?.Entry;

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        var field=Field;
        if(field!=null&&field.Popup==null)field.Popup=this;
        if(!inWorld||field is not {Map.FieldOpen:true}){open=false;pressed=null;}
        // sub_A770F0：提示結束（送出或 ESC 取消）→ 旗標清掉、標記消失。
        if(prompting&&(!inWorld||Entry is not {Prompting:true}))prompting=false;
    }

    public bool ShowMenu(Point cursor,Point? mapPoint,IUiCommands commands){
        if(!inWorld)return false;
        at=new(Math.Min(cursor.X,MaxX)+1,Math.Min(cursor.Y,MaxY)+1);
        if(ButtonRect(SendButton).IsEmpty&&ButtonRect(SaveButton).IsEmpty)return false;   // 缺圖就不畫
        var current=Field?.Map.Current;
        target=current==null?null:mapPoint is Point p?new(current.MapId,p.X,p.Y):new(current.MapId,current.X,current.Y);
        open=true;pressed=null;prompting=false;
        return true;
    }
    public void HideMenu(){open=false;pressed=null;}
    /// <summary>Stack / global ESC close: the menu and the marker (the chat line keeps its own ESC rule).</summary>
    public void Close(){HideMenu();prompting=false;}

    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        foreach(int b in new[]{SendButton,SaveButton}){
            var r=ButtonRect(b);if(r.IsEmpty)continue;
            int surf=b==SendButton?(pressed==b?SendDownSurf:SendSurf):(pressed==b?SaveDownSurf:SaveSurf);
            canvas.DrawSurface(Surf(surf)??Surf(b==SendButton?SendSurf:SaveSurf),r.X,r.Y);
        }
    }
    int? ButtonAt(Point p)=>ButtonRect(SendButton).Contains(p)?SendButton:ButtonRect(SaveButton).Contains(p)?SaveButton:null;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        // 右鍵按在選單上：子鈕不收，Field_Map 會收起選單（sub_A6F0B0 516）。
        if(e.Button==MouseButtons.Right){if(e.Kind==UiMouseKind.Down)HideMenu();return true;}
        if(e.Button!=MouseButtons.Left)return Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Down){pressed=ButtonAt(e.Location);if(pressed==null)HideMenu();return true;}
        if(e.Kind==UiMouseKind.Up){
            int? was=pressed;pressed=null;
            if(was is int b&&ButtonAt(e.Location)==b)Activate(b,commands);
            return true;
        }
        return true;
    }
    /// <summary>sub_A762A0: hide the menu, then button 0 = TransferMenu (1785 on the chat line), button 1 = MapLocationSaveWindow.</summary>
    public bool Activate(int button,IUiCommands commands){
        var point=target;
        HideMenu();
        if(button==SendButton){
            if(Field?.TransferMenu(commands)!=true)return false;
            prompting=true;return true;
        }
        return find?.Invoke(MapLocationSavePanel.PanelId) is MapLocationSavePanel save&&save.Open(point);
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}

/// <summary>
/// R9-B：1785 畫在聊天輸入列上（sub_AC98E0 → sub_AC9600 把提示放進聊天輸入列物件 dword_13CE3F4，用 sub_7025D0(字串,14,14) 畫在輸入列本身）。
/// 引擎的聊天輸入列（ChatPanel）不在本任務可改範圍，所以由這個 Float 面板畫在輸入文字右側（確切 x 推測），範圍只到輸入列右緣；
/// 滑鼠一律轉給聊天面板（不吃輸入列的點擊）。顏色 palette[14]（ChatFormat.White）。
/// </summary>
public sealed class MapSendPromptPanel(Func<string,IUiPanel?>? find=null):IUiClosablePanel {
    public const string PanelId="R9B_MapSendPrompt";
    public const int Gap=6,BarMargin=4;
    [UiPanel(193)] internal static UiPanelRegistration Registration=>new(PanelId,"uistatus86",root=>new MapSendPromptPanel(root.Find));   // R9-B (#846): string 1785 drawn on the chat input line

    bool inWorld,prompting;
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    ChatPanel? Chat=>find?.Invoke(ChatPanel.WindowName) as ChatPanel;
    public bool Prompting=>prompting;
    public bool Visible=>inWorld&&prompting;
    public Rectangle Bounds=>prompting&&Chat is {} chat?PromptRect(chat.Drawn):Rectangle.Empty;

    /// <summary>1785 from string-c.tbl (LocationShareProtocol.Strings); numbered placeholder without the table.</summary>
    public static string Text=>LocationShareProtocol.Strings is {} t&&LocationShareProtocol.PromptMessageId<t.Count
        &&ServerMessageText.DisplayText(t[LocationShareProtocol.PromptMessageId]).Trim() is {Length:>0} s?s:$"〔訊息{LocationShareProtocol.PromptMessageId}〕";

    /// <summary>Right of the drawn input text, up to the bar's right edge minus 4; empty without a drawn bar / input.</summary>
    public static Rectangle PromptRect(IReadOnlyList<ChatDraw> drawn){
        var bar=drawn.LastOrDefault(d=>d.Element==ChatPanel.Bar);var input=drawn.LastOrDefault(d=>d.Element=="INPUT");
        if(bar==null||input==null)return Rectangle.Empty;
        int x=input.Rect.Right+Gap,right=bar.Rect.Right-BarMargin;
        return right>x?new(x,input.Rect.Y,right-x,ChatRules.LineHeight):Rectangle.Empty;
    }

    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        prompting=inWorld&&(find?.Invoke(LocationListPanel.PanelId) as LocationListPanel)?.Entry is {Prompting:true};
    }
    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        var b=Bounds;if(b.IsEmpty)return;
        canvas.DrawText(Text,b.X,b.Y,ChatPalette.Color(ChatFormat.White),Color.Black);
    }
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>Chat?.Mouse(e,commands)??false;
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    /// <summary>The prompt follows the chat line (ESC there ends it); nothing to close here.</summary>
    public void Close(){}
}
