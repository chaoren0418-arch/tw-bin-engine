using P06;
using P06.NativeUi;
using System.Drawing.Imaging;

// #1207：合成 actor tiles／surf，驗證實際頭上條與整張畫布呈現；不讀遊戲素材、不開視窗或網路。
partial class Checks {
    [CheckGroup("headbar-coordinates"),CheckGroup("native-ui-canvas",Order=20)]
    static void HeadBarCoordinatesChecks(string repo){
        using var resources=HpMpSyntheticResources(Path.Combine(repo,"scratch","issue-1207","headbar-fixture"),new Dictionary<int,(int,int,ushort)>{
            [930]=(44,5,HmBase),[931]=(44,5,HmHp),[932]=(44,5,HmMp),[8393]=(44,4,HmWarn)});
        foreach(float zoom in new[]{1f,1.5f,2f})
        foreach(var world in new[]{UiRoot.WorldRect,new Rectangle(37,29,700,520)})
        foreach(bool self in new[]{true,false}){
            var camera=new Camera();camera.SetCenter(new(560.25f,946.5f));camera.Scale(zoom/camera.Zoom,PointF.Empty);
            Point? previous=null;Rectangle? previousBox=null;
            // 三個顯示影格：站立／步行中／下一格；外框直接來自 body Draw，而不是另算的腳下插值。
            foreach(int step in new[]{0,1,2}){
                var tile=new Tile(0,[]);
                Draw[] body=[new(20+step,10,0,0,"actor",0,-1,-1,tile),new(21+step,10,0,1,"actor",1,-1,-1,tile)];
                var bounds=Rectangle.FromLTRB(body.Min(d=>d.Px),body.Min(d=>d.Py),body.Max(d=>d.Px+24),body.Max(d=>d.Py+24));
                // 模擬狀態收集早於 PaintScene，鏡頭尚留著舊視窗的 viewport。
                camera.View=new(1600,1200);camera.ViewOffset=new(91,83);
                var target=HpMpHeadTarget.FromScene(self?1u:2u,self,bounds,camera,world,self?null:50);
                Check(camera.View==new Size(1600,1200)&&camera.ViewOffset==new PointF(91,83),"headbar projection does not mutate camera");
                Check(target.Origin==null,"product does not reinterpret the engine foot as original-client self P");

                // 身體沿用 PaintScene 的 Camera.ToView；與先前的狀態必須一致。
                camera.View=world.Size;camera.ViewOffset=world.Location;
                var topLeft=camera.ToView(bounds.Location);var bottomRight=camera.ToView(new(bounds.Right,bounds.Bottom));
                var rendered=Rectangle.FromLTRB((int)Math.Floor(topLeft.X),(int)Math.Floor(topLeft.Y),(int)Math.Floor(bottomRight.X),(int)Math.Floor(bottomRight.Y));
                Check(target.Box==rendered,$"body/head viewport agrees: zoom={zoom}, world={world}, self={self}, frame={step}");
                var heads=new HpMpHeadBarPanel();heads.Load(null!,resources.Surfs);
                UiHpMpGameState State(long now)=>new(new UiStaticGameState(true,"fixture",1,10,100,50,100,now),[target]);
                heads.Update(State(0));var plan=heads.Plan().Single();
                var bodyOrigin=new Point(rendered.Left+rendered.Width/2,rendered.Top);
                var expected=new Point(bodyOrigin.X-24,bodyOrigin.Y-12);
                Check(plan.At==expected,"original bbox offset remains (-24,-12) logical pixels at every camera zoom");
                Check(plan.Hp==(self?10:50)&&plan.Mp==(self?50:null),"own HP/MP and other HP-only rules preserved");
                if(previous is Point old&&previousBox is Rectangle oldBox){
                    Check(plan.At.X-old.X==bodyOrigin.X-(oldBox.Left+oldBox.Width/2)&&plan.At.Y-old.Y==rendered.Top-oldBox.Top,
                        "walking headbar follows the displayed body frame, including bbox rounding");
                }
                previous=plan.At;previousBox=rendered;
                using var canvas=new UiCanvas();canvas.Clear(Color.Black);
                canvas.Graphics.FillRectangle(Brushes.White,rendered);heads.Paint(canvas);
                Check(canvas.Bitmap.GetPixel(expected.X,expected.Y).ToArgb()==HmArgb(HmHp),"HP surface begins at the expected body-relative origin");
                Check(canvas.Bitmap.GetPixel(expected.X,expected.Y+4).ToArgb()==HmArgb(self?HmMp:HmBase),"MP row remains attached to HP; others have no MP");
                heads.Update(State(301));var blinking=heads.Plan().Single();
                Check(blinking.At==expected&&blinking.Blink==self,"low HP blink does not move the origin and applies only to self");
                heads.Paint(canvas);
                int barColor=HmArgb(self?HmWarn:HmHp);
                Check(canvas.Bitmap.GetPixel(expected.X,expected.Y).ToArgb()==barColor,"blink overlay is painted at the same origin");

                // 兩種實際 Graphics DPI，含 1×、2× 與帶黑邊的非整數呈現；DPI 不參與 Camera 投影。
                foreach(int dpi in new[]{96,192})
                foreach(var presentation in new[]{(Size:new Size(800,600),Fill:false),(Size:new Size(1600,1200),Fill:false),(Size:new Size(1100,800),Fill:true)}){
                    using var output=new Bitmap(presentation.Size.Width,presentation.Size.Height,PixelFormat.Format32bppPArgb);
                    output.SetResolution(dpi,dpi);
                    var viewport=UiPresenter.Compute(presentation.Size,presentation.Fill);
                    using(var graphics=Graphics.FromImage(output)){
                        Check(graphics.DpiX==dpi&&graphics.DpiY==dpi,"DPI fixture is effective");
                        UiPresenter.Present(graphics,canvas.Bitmap,presentation.Size,viewport);
                    }
                    // 取原點內側像素避免非整數最近鄰邊界取整差異；兩個取樣點走同一次整張畫布縮放。
                    var barPixel=viewport.ToClient(new(expected.X+1,expected.Y+1));
                    var bodyPixel=viewport.ToClient(new(bodyOrigin.X,bodyOrigin.Y+1));
                    Check(output.GetPixel(barPixel.X,barPixel.Y).ToArgb()==barColor&&output.GetPixel(bodyPixel.X,bodyPixel.Y).ToArgb()==Color.White.ToArgb(),
                        $"body and headbar survive one presentation transform: DPI={dpi}, size={presentation.Size}, fill={presentation.Fill}");
                }
            }
        }
    }
}
