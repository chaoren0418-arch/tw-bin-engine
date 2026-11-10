namespace P06.NativeUi;

// R9-B（T362／#846）：存點窗 MapLocationSaveWindow（docs/reference/followups-9.md §7.2），由地圖右鍵選單鈕 1 開啟。
// 已驗證（IDA，TW16 IDB SHA-256 3a0bf206…7e82f，見 docs/reports/issue-846.md）：
// - 建立 sub_A62C40（VA 0xA62C40／FO 0x662040）：底 surf 134 置中在 ((800−w)/2, (530−h)/2)；確認鈕 2951（按下 2953）在 (124,104)，
//   回呼 sub_A77080；輸入框 sub_AC3D90 在 (12,108) 110×16，上限 12 字（sub_703EA0(12)），字色 palette[14]。
// - 繪製 sub_A6AA80：底 134 以 sub_7D7740(…,23) 半透明畫；選取框 surf 138 畫在 (11+19×(i%8), 43+19×(i/8))；再畫子控制項。
// - 滑鼠 sub_A6E710：只收底圖範圍內的事件；左鍵按下（513）落在調色盤 (11,43)–(w155+8, h155+40) 內 →
//   i＝(x−11)/19＋8×((y−43)/19)（surf 155 只用來量調色盤範圍，不畫）。
// - 鍵盤 sub_A6E6A0：Enter＝sub_A77080（確認），ESC＝關窗；其他交給輸入框。
// - 顯示 sub_A66A10：已開著就不動；否則顯示並把焦點給輸入框。隱藏 sub_A687B0。
// - **sub_A77080 只把輸入框讀進區域緩衝區（25 byte）就關窗，沒有存到任何地方**；選單算出的游標地圖座標（sub_A66F50）
//   也只放在 sub_A6F0B0 的區域變數。所以舊式路徑不存點、Field_Map 上也不畫存點（MapUI 版的 `<角色名>.loc`、ShowBookmark
//   只在本機沒有的 MapUI.xml 路徑，FU9 §7.1）。
// 引擎做法（標明推測）：
// - 確認時把（地圖、座標、顏色、備註）存進本地清單 MapLocationBook：每角色、上限 30 筆、滿了顯示 1786（規則取自 MapUI 路徑
//   sub_8D40C0 的 30 筆上限，FU9 §7.1）。清單只在記憶體（本次執行期間）；寫檔（.loc 格式）與在地圖上畫存點都待後續。
// - 底圖不做半透明（同 ClanWarehouseHistoryPanel：引擎沒有透明度設定 dword_1372264）。
// - 1786 滿額時窗不關、在輸入框上方顯示 1786（位置推測）；舊式路徑一律關窗。
// - 缺圖就不畫：沒有 134 時窗不開；沒有 138／155／2951 時各自不畫、不能點。
// - 原生 UI 關閉（--classic-ui）時沒有這個窗（舊式 WinForms HUD 沒有存點功能）。

/// <summary>One saved point (local only, no packet).</summary>
public sealed record MapLocationMark(int MapId,int X,int Y,int Color,string Memo);

/// <summary>
/// Local point list per character (engine, in memory). Max 30 like the MapUI path (sub_8D40C0); a full list refuses with 1786.
/// </summary>
public sealed class MapLocationBook {
    public const int MaxPerCharacter=30;
    readonly Dictionary<string,List<MapLocationMark>> marks=new(StringComparer.Ordinal);
    public IReadOnlyList<MapLocationMark> For(string character)=>marks.TryGetValue(character,out var list)?list:[];
    /// <summary>false = the character already has 30 points (nothing stored).</summary>
    public bool Add(string character,MapLocationMark mark){
        if(!marks.TryGetValue(character,out var list))marks[character]=list=[];
        if(list.Count>=MaxPerCharacter)return false;
        list.Add(mark);return true;
    }
}

public sealed class MapLocationSavePanel(MapLocationBook? book=null):IUiClosablePanel,IUiTextInput {
    public const string PanelId="MapLocationSaveWindow";
    public const int BaseSurf=134,SelectorSurf=138,PaletteSurf=155,ConfirmSurf=2951,ConfirmDownSurf=2953;
    public const int CanvasWidth=800,CanvasHeight=530,Cell=19,Columns=8,MaxMemo=12,FullMessageId=1786;
    public static readonly Point PaletteAt=new(11,43),ConfirmAt=new(124,104),EditAt=new(12,108),FullAt=new(12,90);
    public static readonly Size EditSize=new(110,16);
    // T270（#645）自動發現：註冊宣告在面板旁，不改 UiPanelRegistry.cs。
    [UiPanel(194)] internal static UiPanelRegistration Registration=>new(PanelId,"uistatus86",root=>new MapLocationSavePanel());   // R9-B (#846): map point save window, layout hard-coded in the BIN (sub_A62C40)

    readonly MapLocationBook book=book??new MapLocationBook();
    readonly System.Text.StringBuilder memo=new();
    Func<int,UiSurface?>? surfs;
    bool inWorld,open,confirmPressed,full;string? character;
    MapLocationTarget? target;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public bool Visible=>inWorld&&open;
    public MapLocationBook Book=>book;
    /// <summary>Selected palette index (this+12; 0 when the window is built).</summary>
    public int Color{get;private set;}
    public string Memo=>memo.ToString();
    public MapLocationTarget? Target=>target;
    /// <summary>true after a confirm the 30-point list refused (1786 shown until the next edit or close).</summary>
    public bool ShowsFull=>full;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    /// <summary>Same as Load(layout, catalog) with any surface source (offline checks use in-memory surfaces).</summary>
    public void Load(Func<int,UiSurface?> surfs)=>this.surfs=surfs;
    UiSurface? Surf(int id)=>surfs?.Invoke(id);

    /// <summary>((800 − w) / 2, (530 − h) / 2) of surf 134, integer halves like the original &gt;&gt; 1.</summary>
    public Point Origin=>Surf(BaseSurf) is {} s?new((CanvasWidth-s.Width)>>1,(CanvasHeight-s.Height)>>1):Point.Empty;
    public Rectangle Bounds=>open&&Surf(BaseSurf) is {} s?new(Origin,new(s.Width,s.Height)):Rectangle.Empty;
    Rectangle At(Point rel,Size size)=>new(Origin.X+rel.X,Origin.Y+rel.Y,size.Width,size.Height);
    public Rectangle ConfirmRect=>Surf(ConfirmSurf) is {} s?At(ConfirmAt,new(s.Width,s.Height)):Rectangle.Empty;
    public Rectangle EditRect=>At(EditAt,EditSize);
    /// <summary>sub_A6E710's palette rectangle: (11,43) to (w155 + 8, h155 + 40); empty without surf 155.</summary>
    public Rectangle PaletteRect=>Surf(PaletteSurf) is {} s?Rectangle.FromLTRB(Origin.X+PaletteAt.X,Origin.Y+PaletteAt.Y,Origin.X+s.Width+8,Origin.Y+s.Height+40):Rectangle.Empty;
    /// <summary>Top-left of the selector (surf 138) for a palette index.</summary>
    public Point SelectorAt(int index)=>new(Origin.X+PaletteAt.X+Cell*(index%Columns),Origin.Y+PaletteAt.Y+Cell*(index/Columns));

    /// <summary>sub_A66A10 (menu button 1). Already open → unchanged. false = no surf 134 (nothing to draw) or not in the world.</summary>
    public bool Open(MapLocationTarget? point){
        if(!inWorld||Surf(BaseSurf)==null)return false;
        if(open)return true;
        open=true;target=point;memo.Clear();full=false;confirmPressed=false;
        return true;
    }
    /// <summary>sub_A687B0; also the stack close (pushed out, global ESC).</summary>
    public void Close(){open=false;confirmPressed=false;full=false;memo.Clear();}

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        if(state.CharacterName!=character){character=state.CharacterName;if(open)Close();}
        if(!inWorld&&open)Close();
    }

    /// <summary>
    /// sub_A77080 (Enter / confirm button). Original: reads the edit and closes, nothing stored. Engine: store the point in the
    /// local list first; a full list keeps the window open with 1786. false = nothing stored.
    /// </summary>
    public bool Confirm(IUiCommands commands){
        if(!Visible)return false;
        bool stored=false;
        if(character is {Length:>0} name&&target is {} t){
            stored=book.Add(name,new(t.MapId,t.X,t.Y,Color,memo.ToString()));
            if(!stored){full=true;return false;}
        }
        Close();commands.ReturnGameFocus();
        return stored;
    }

    public static string FullText=>LocationShareProtocol.Strings is {} t&&FullMessageId<t.Count
        &&ServerMessageText.DisplayText(t[FullMessageId]).Trim() is {Length:>0} s?s:$"〔訊息{FullMessageId}〕";

    public void Paint(UiCanvas canvas){
        if(!Visible||Surf(BaseSurf) is not {} b)return;
        var o=Origin;
        canvas.DrawSurface(b,o.X,o.Y);
        if(Surf(SelectorSurf) is {} sel){var p=SelectorAt(Color);canvas.DrawSurface(sel,p.X,p.Y);}
        if((Surf(confirmPressed?ConfirmDownSurf:ConfirmSurf)??Surf(ConfirmSurf)) is {} c){var r=ConfirmRect;canvas.DrawSurface(c,r.X,r.Y);}
        var e=EditRect;
        canvas.DrawText(memo+"_",e.X,e.Y,ChatPalette.Color(ChatFormat.White),System.Drawing.Color.Black);
        if(full)canvas.DrawText(FullText,o.X+FullAt.X,o.Y+FullAt.Y,ChatPalette.Color(ChatFormat.White),System.Drawing.Color.Black);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(!Bounds.Contains(e.Location)&&e.Kind==UiMouseKind.Down)return false;
        if(e.Button!=MouseButtons.Left)return true;
        if(e.Kind==UiMouseKind.Down){
            if(ConfirmRect.Contains(e.Location)){confirmPressed=true;return true;}
            var pal=PaletteRect;
            if(pal.Contains(e.Location))Color=(e.X-(Origin.X+PaletteAt.X))/Cell+Columns*((e.Y-(Origin.Y+PaletteAt.Y))/Cell);
            return true;
        }
        if(e.Kind==UiMouseKind.Up){
            bool was=confirmPressed;confirmPressed=false;
            if(was&&ConfirmRect.Contains(e.Location))Confirm(commands);
        }
        return true;
    }

    /// <summary>Typed characters (WM_CHAR after IME) go to the 12-character edit.</summary>
    public bool Char(char c){
        if(!Visible||char.IsControl(c))return false;
        if(memo.Length<MaxMemo)memo.Append(c);
        full=false;return true;
    }
    /// <summary>sub_A6E6A0: Enter = confirm, ESC = close; Backspace edits. Printable keys arrive through Char.</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(!e.Down)return e.KeyCode is Keys.Enter or Keys.Escape or Keys.Back;
        if(e.KeyData==Keys.Enter){if(!e.Repeat)Confirm(commands);return true;}
        if(e.KeyData==Keys.Escape){Close();commands.ReturnGameFocus();return true;}
        if(e.KeyData==Keys.Back){if(memo.Length>0)memo.Length--;full=false;return true;}
        return false;
    }
}
