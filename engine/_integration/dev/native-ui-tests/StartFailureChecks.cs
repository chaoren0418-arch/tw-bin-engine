using P06;
using P06.NativeUi;

// f475-ui-fail-message (Issue #1097, #475 SM01). Offline only: synthetic archives under scratch/issue-1097/, no real client.
// The red fallback line and the window-events.json failed category come from UiRoot.DescribeStartFailure:
// a panel layout error must not be reported as a resource pin mismatch.
partial class Checks {
    /// <summary>Mimics SpellWindowPanel on #475 SM01: the layout loads but the panel finds no room for its list.</summary>
    sealed class NoRoomPanel:IUiPanel {
        public string Id=>"SPELL_WINDOW";public UiLayer Layer=>UiLayer.Window;public Rectangle Bounds=>Rectangle.Empty;public bool Visible=>false;
        public void Load(UiLayout layout,SurfCatalog surfs){
            var n=layout.Root.Child("SPELL_WINDOW");
            throw new UiLayoutException(layout.FileName,n.Path,"魔法清單沒有空間");
        }
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }

    [CheckGroup("native-ui-start-failure"),CheckGroup("native-ui-canvas",Order=50),CheckGroup(CheckGroups.Default,Order=44)]
    static void NativeUiStartFailureChecks(string repo){
        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1097","synthetic");
        Directory.CreateDirectory(dir);foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        string surfList="<?xml version=\"1.0\"?><SurfList><surf id=\"42\" file=\"42\"/></SurfList>";
        string layout="<?xml version=\"1.0\"?><Magic><SPELL_WINDOW x=\"0\" y=\"0\"/></Magic>";
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)),("uimagicitem-c.xml",Xml(layout)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),($"pad{i}.img",Img(1,1,0,0)))).ToArray();

        // (a) a panel Load throwing UiLayoutException → layout line with file name and window id, category=layout.
        UiLayoutException? caught=null;
        using(var res=new UiResources(dir,tilePin,spritePins)){
            try{UiRoot.Build(res,[new UiPanelRegistration("SPELL_WINDOW","uimagicitem",_=>new NoRoomPanel())]);}
            catch(UiLayoutException ex){caught=ex;}
        }
        Check(caught!=null,"Build: panel layout error propagates as UiLayoutException (all-or-nothing unchanged)");
        Check(caught!.PanelId=="SPELL_WINDOW"&&caught.File=="uimagicitem-c.xml"&&caught.Reason=="魔法清單沒有空間","Build: layout exception names the registered panel, file and bare reason");
        Check(caught is UiResourceException,"UiLayoutException still is a UiResourceException (catch sites unchanged)");
        var a=UiRoot.DescribeStartFailure(caught);
        Check(a.Category=="layout","(a) layout exception → category layout: "+a.Category);
        Check(a.Message=="原生 UI 版面載入失敗（uimagicitem-c.xml:SPELL_WINDOW）：魔法清單沒有空間，已使用舊介面","(a) layout message with file and window: "+a.Message);
        Check(a.Message!=UiRoot.PinMismatchMessage&&!a.Message.Contains("鎖定版本"),"(a) layout error is not reported as pin mismatch");

        // XML that does not parse (no panel ran yet) is still a layout error, located by file only.
        var parse=new UiLayoutException("uix.xml","","XML 語法錯誤（行 3）");
        var p=UiRoot.DescribeStartFailure(parse);
        Check(p.Category=="layout"&&p.Message=="原生 UI 版面載入失敗（uix.xml）：XML 語法錯誤（行 3），已使用舊介面","layout error without panel/node: "+p.Message);
        // Deep node path under a known panel keeps both.
        var deep=new UiLayoutException("uix.xml","Root/Window/Items_List","沒有清單列"){PanelId="AGIT"};
        Check(UiRoot.DescribeStartFailure(deep).Message=="原生 UI 版面載入失敗（uix.xml:AGIT）：Root/Window/Items_List: 沒有清單列，已使用舊介面","layout error keeps the node path when it differs from the panel id");

        // (b) a real pin / anchor mismatch (plain UiResourceException) → the original pin line, category=pin.
        const int k=3;
        var badAnchor=spritePins.Select((pin,i)=>i==k?pin with{Anchors=[new($"pad{k}.img",new string('f',64))]}:pin).ToArray();
        UiResourceException? pinError=null;
        using(var res=new UiResources(dir,tilePin,badAnchor)){
            try{UiRoot.Build(res,[]);}catch(UiResourceException ex){pinError=ex;}
        }
        Check(pinError!=null&&pinError is not UiLayoutException,"anchor mismatch throws a plain UiResourceException");
        var b=UiRoot.DescribeStartFailure(pinError);
        Check(b.Category=="pin"&&b.Message==UiRoot.PinMismatchMessage,"(b) pin mismatch → original pin message: "+b.Message);
        Check(UiRoot.DescribeStartFailure(new UiResourceException("Sprite09: 缺少 idx")).Category=="pin","(b) container check failure → pin");

        // (c) anything else → generic line, category=other.
        var c=UiRoot.DescribeStartFailure(new InvalidOperationException("註冊 X 建立出的面板 Id 是 Y"));
        Check(c.Category=="other"&&c.Message=="原生 UI 無法啟動，已使用舊介面：註冊 X 建立出的面板 Id 是 Y","(c) other exception → generic message: "+c.Message);
        Check(c.Message.StartsWith(UiRoot.GenericFailurePrefix,StringComparison.Ordinal),"(c) generic prefix constant");
        Check(UiRoot.DescribeStartFailure(null).Category=="other","null error (no root) → other");
    }
}
