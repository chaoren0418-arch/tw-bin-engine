using P06.NativeUi;
namespace P06;

// T236 / W2-E（#558）：聊天輸入列 YES／NO 鈕的離線測試（純座標與回覆封包，不連遊戲、網路或素材）。由 --module-tests 執行。
static class YesNoButtonsTests {
    [WorldModuleTest("yes-no-buttons")]
    static void Run(WorldModuleTestContext t){
        // windows-2.md §5.3：YES (361, 567)、NO (401, 567)，800×600 畫面座標。
        t.Equal(new Point(361,567),YesNoButtons.Logical(true).Location,"YES at (361,567)");
        t.Equal(new Point(401,567),YesNoButtons.Logical(false).Location,"NO at (401,567)");
        t.Equal("YES",YesNoButtons.Label(true),"YES label");
        t.Equal("NO",YesNoButtons.Label(false),"NO label");
        t.Check(!YesNoButtons.Logical(true).IntersectsWith(YesNoButtons.Logical(false)),"buttons do not overlap");
        t.Check(UiRoot.Canvas.Contains(YesNoButtons.Logical(true))&&UiRoot.Canvas.Contains(YesNoButtons.Logical(false)),"buttons inside 800x600");

        // 命中測試（邏輯座標）。
        t.Equal((bool?)true,YesNoButtons.Hit(new(361,567)),"hit YES top-left");
        t.Equal((bool?)true,YesNoButtons.Hit(new(361+YesNoButtons.Size.Width-1,567+YesNoButtons.Size.Height-1)),"hit YES bottom-right");
        t.Equal((bool?)false,YesNoButtons.Hit(new(401,567)),"hit NO top-left");
        t.Equal((bool?)null,YesNoButtons.Hit(new(360,567)),"left of YES misses");
        t.Equal((bool?)null,YesNoButtons.Hit(new(401,566)),"above NO misses");
        t.Equal((bool?)null,YesNoButtons.Hit(new(401+YesNoButtons.Size.Width,567)),"right of NO misses");

        // 原生畫布：照 UiPresenter 的整數倍置中換算到客戶區。
        var one=UiPresenter.Compute(new(800,600));
        t.Equal(YesNoButtons.Logical(true),YesNoButtons.Client(true,one),"s=1 client = logical");
        var two=UiPresenter.Compute(new(1600,1200));
        t.Equal(new Rectangle(722,1134,64,28),YesNoButtons.Client(true,two),"s=2 YES scaled");
        t.Equal(new Rectangle(802,1134,64,28),YesNoButtons.Client(false,two),"s=2 NO scaled");
        var wide=UiPresenter.Compute(new(1920,1080));
        t.Equal(new Rectangle(560+361,240+567,32,14),YesNoButtons.Client(true,wide),"1920x1080 s=1 centred offset");
        foreach(var (size,label) in new[]{(new Size(800,600),"800x600"),(new Size(1600,1200),"1600x1200"),(new Size(1920,1080),"1920x1080"),(new Size(1000,700),"1000x700 fill")}){
            var v=UiPresenter.Compute(size,label.EndsWith("fill"));
            foreach(bool yes in new[]{true,false}){
                var c=YesNoButtons.Client(yes,v);var center=new Point(c.X+c.Width/2,c.Y+c.Height/2);
                t.Equal((bool?)yes,v.ToLogical(center) is Point p?YesNoButtons.Hit(p):null,$"{label} client centre maps back to {YesNoButtons.Label(yes)}");
            }
        }

        // 舊介面：保留原版順序與 40 px 間距，放在提示列右端、垂直置中、不超出提示列。
        var line=new Rectangle(8,500,640,24);
        var ly=YesNoButtons.Legacy(true,line);var ln=YesNoButtons.Legacy(false,line);
        t.Equal(40,ln.X-ly.X,"legacy pitch 40");
        t.Check(line.Contains(ly)&&line.Contains(ln),"legacy buttons inside prompt line");
        t.Equal(line.Y+(line.Height-YesNoButtons.Size.Height)/2,ly.Y,"legacy vertical centre");

        // 按鈕與 Y／N＋Enter 走同一個 C121：H0 D(request) H(message) C(1／0)。
        t.Check(YesNoProtocol.Answer(0,1000,true).SequenceEqual(LoginPackets.Pad([121,0,0,0,0,0,0,0xE8,0x03,1])),"YES → C121 accept");
        t.Check(YesNoProtocol.Answer(0,1000,false).SequenceEqual(LoginPackets.Pad([121,0,0,0,0,0,0,0xE8,0x03,0])),"NO → C121 decline");
    }
}
