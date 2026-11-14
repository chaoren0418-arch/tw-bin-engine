using P06.NativeUi;
using System.Drawing.Imaging;

// #1205 (f1186-uitext-pixel): native UI text must be sized in canvas pixels, independent of the canvas DPI.
// Program.Main runs PerMonitorV2, so a new Bitmap (the 800×600 canvas, the HyperText measuring bitmap) carries the monitor
// DPI; the old 9 pt font was 12 px at DPI 96 but 24 px at DPI 192 (#1186 SM10/12/16/18/20/33).
// Offline only: in-memory bitmaps with SetResolution(96/192), no client resources, no window.
partial class Checks {
    [CheckGroup("native-ui-text-dpi"),CheckGroup("native-ui-canvas",Order=40),CheckGroup(CheckGroups.Default,Order=44)]
    static void NativeUiTextDpiChecks(string repo){
        // --- font definition: original CreateFontA(12,0,0,0,400,0,0,0,136,…,"MingLiU") → 12 px, Pixel unit, Big5 charset.
        var f=UiText.Font;
        Check(f.Unit==GraphicsUnit.Pixel&&f.Size==UiText.PixelHeight&&UiText.PixelHeight==12,$"UiText font is {UiText.PixelHeight} px (GraphicsUnit.Pixel), not points: {f.Size} {f.Unit}");
        Check(f.GdiCharSet==136&&!f.Bold&&!f.Italic,"UiText font: CHINESEBIG5 (136), FW_NORMAL");
        Check(UiText.FaceName=="MingLiU","UiText face follows the original CreateFontA face MingLiU");
        bool mingLiU=UiText.FaceUsed is "MingLiU" or "細明體";
        Console.WriteLine($"[native-ui-text-dpi] face used: {UiText.FaceUsed} (MingLiU installed: {mingLiU})");
        Check(UiText.PixelHeight==ChatRules.LineHeight,"UiText pixel height = chat row pitch (12)");

        // --- the harness really changes DPI: a 9 pt font (the old definition) doubles from 96 to 192.
        using(var c96=Canvas(96))using(var c192=Canvas(192))using(var old=new Font(f.FontFamily,9f,FontStyle.Regular,GraphicsUnit.Point)){
            Check(c96.G.DpiY==96&&c192.G.DpiY==192,$"canvas Graphics DPI follows SetResolution: {c96.G.DpiY}/{c192.G.DpiY}");
            float h96=old.GetHeight(c96.G),h192=old.GetHeight(c192.G);
            Check(h192>h96*1.9f,$"control: a 9 pt font is {h96:0.#} px at DPI 96 but {h192:0.#} px at DPI 192 (the #1186 defect)");
        }

        // --- same string, DPI 96 vs 192: identical measured size and identical ink box.
        string[] samples=["天堂","國","A","1234/1234","32767/32767","100%","99%","光箭","99999","[測試者] 你好，這是一行聊天訊息。","說明：Ctrl+H 文字"];
        foreach(var s in samples){
            using var c96=Canvas(96);using var c192=Canvas(192);
            var m96=UiText.Measure(c96.G,s);var m192=UiText.Measure(c192.G,s);
            var i96=Ink(c96,s);var i192=Ink(c192,s);
            Console.WriteLine($"[native-ui-text-dpi] \"{s}\": measure 96={m96.Width}×{m96.Height} 192={m192.Width}×{m192.Height}; ink 96={i96.Width}×{i96.Height} 192={i192.Width}×{i192.Height}");
            Check(m96==m192,$"\"{s}\": measured size equal at DPI 96 and 192 ({m96.Width}×{m96.Height} / {m192.Width}×{m192.Height})");
            Check(i96.Size==i192.Size,$"\"{s}\": ink box equal at DPI 96 and 192 ({i96.Width}×{i96.Height} / {i192.Width}×{i192.Height})");
            Check(i192.Height<=UiText.PixelHeight,$"\"{s}\": ink height {i192.Height} ≤ original cell height {UiText.PixelHeight} px at DPI 192");
            Check(i192.Width<=m192.Width+1,$"\"{s}\": ink width {i192.Width} within the measured width {m192.Width} (+1 px grid fit)");
        }

        using var g96=Canvas(96);using var g192=Canvas(192);
        int W(string s)=>UiText.Measure(g192.G,s).Width;
        int InkH(string s)=>Ink(g192,s).Height;
        if(mingLiU){
            // MingLiU is fixed pitch: half width 512/1024 em, full width 1024/1024 em → 6 / 12 px (as NameLayout and the GDI path).
            Check(W("A")==6&&W("國")==12,$"MingLiU: half width 6 px, full width 12 px ({W("A")}/{W("國")})");
            Check(W("測試")==2*W("測")&&W("1234/1234")==9*W("1"),"MingLiU: per-character widths add up (chat runs measure one character at a time)");
        }

        // --- cells. HUD values: HpMpLayout V1 tables (docs/reference/ui-layout.md §2 V1, HpMpPanel.cs). Narrowest bar is
        //     horizontal/normal/status 0: HP 85 × 20, MP 80 × 20; thick bars are 14 high, vertical 15.
        foreach(int shape in new[]{0,1})foreach(int status in new[]{0,1,2})foreach(bool thick in new[]{false,true}){
            var L=HpMpLayout.For(shape,status,thick);
            foreach(var bar in new[]{L.HpBar,L.MpBar}){
                string tag=$"HP/MP shape {shape} status {status} {(thick?"thick":"normal")} bar {bar.Width}×{bar.Height}";
                Check(W("32767/32767")<=bar.Width,$"{tag}: value \"32767/32767\" {W("32767/32767")} px fits the bar width");
                Check(InkH("32767/32767")<=bar.Height,$"{tag}: value ink height {InkH("32767/32767")} ≤ bar height");
                // Percent sits at the bar's left (+7) or right (−tw+4) edge (HpMpPanel.Paint).
                Check(7+W("100%")<=bar.Width&&InkH("100%")<=Math.Max(bar.Height,UiText.PixelHeight),$"{tag}: percent \"100%\" {W("100%")} px fits the bar");
            }
        }
        // Chat: text area x 30–344 (ChatRules, ChatState.cs:38), 12 px rows. Ink stays inside one row at DPI 192.
        int chatWidth=ChatRules.TextRight-ChatRules.TextLeft;
        Check(InkH("[測試者] 你好，這是一行聊天訊息。")<=ChatRules.LineHeight,$"chat: one row ink height {InkH("[測試者] 你好，這是一行聊天訊息。")} ≤ row pitch {ChatRules.LineHeight}");
        if(mingLiU)Check(W(new string('測',chatWidth/12))<=chatWidth,$"chat: {chatWidth/12} full-width characters fit the {chatWidth} px text area");
        // Inventory: 33 × 33 cells (LegacyCellGrid.IconPitch, ItemIconSource.cs); the count is right-aligned at the cell bottom.
        Check(W("99999")<=LegacyCellGrid.IconPitch&&InkH("99999")<=UiText.PixelHeight,$"inventory: count \"99999\" {W("99999")} px fits a {LegacyCellGrid.IconPitch} px cell");
        // Spell window: rows 145 × 16 (local uimagicitem.xml: base x 6 + level strip 14 at x 12 → x 26, scrollbar x 171;
        // SpellWindowPanel.RowPitch). Name at +3, MP right-aligned at −3 with 6 px gap; text at +2.
        const int spellRow=171-26;
        Check(3+W("光箭")+6+W("10")+3<=spellRow,$"spell: \"光箭\" {W("光箭")} px + MP fit a {spellRow} px row");
        Check(2+InkH("光箭")<=SpellWindowPanel.RowPitch,$"spell: name ink {InkH("光箭")} px at +2 stays inside the {SpellWindowPanel.RowPitch} px row");
    }

    sealed class DpiCanvas(Bitmap bitmap):IDisposable {
        public Bitmap Bitmap{get;}=bitmap;
        public Graphics G{get;}=Graphics.FromImage(bitmap);
        public void Dispose(){G.Dispose();Bitmap.Dispose();}
    }
    static DpiCanvas Canvas(float dpi){
        var b=new Bitmap(400,40,PixelFormat.Format32bppPArgb);b.SetResolution(dpi,dpi);
        var c=new DpiCanvas(b);c.G.Clear(Color.Black);return c;
    }
    /// <summary>Bounding box of the pixels UiText.Draw paints (white on black) at (4, 4); Empty when nothing is drawn.</summary>
    static Rectangle Ink(DpiCanvas c,string text){
        c.G.Clear(Color.Black);UiText.Draw(c.G,text,4,4,Color.White);
        int l=int.MaxValue,t=int.MaxValue,r=-1,btm=-1;
        for(int y=0;y<c.Bitmap.Height;y++)for(int x=0;x<c.Bitmap.Width;x++){
            var p=c.Bitmap.GetPixel(x,y);if(p.R==0&&p.G==0&&p.B==0)continue;
            l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);btm=Math.Max(btm,y);
        }
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,btm+1);
    }
}
