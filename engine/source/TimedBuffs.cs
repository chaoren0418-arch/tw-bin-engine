using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace P06;
// Server formats and the verified/inferred split: docs/protocol/timed-buffs.md.
public static class TimedBuffRules {
    public const int PhysicalEnchantStr=42,DressMighty=109,PhysicalEnchantDex=26,DressDexterity=110;
    // Item status with no skill id; negative keys are the MagicStatus convention for those.
    public const int WaterBreath=-3126;
    // AM§4(c), TW16 sub_F01E50: after lighting, screen row j moves horizontally by
    // (int)(level*3.5*sin((phase+j)*pi/50)). Phase advances every 50 ms through 0..199 (sub_F05FD0),
    // so the 100-row wave travels one period in 5 s and returns home at phase 200.
    public const int LiquorTickMs=50,LiquorPhases=200;
    public static int LiquorPhase(long elapsedMs)=>(int)(Math.Max(0,elapsedMs)/LiquorTickMs%LiquorPhases);
    public static int ScanlineOffset(int level,int phase,int row)=>level is 1 or 2 or 3?(int)(level*3.5*Math.Sin((phase+row)*Math.PI/50)):0;
    // Shift each row of a 32-bit frame in place. Uncovered edge columns repeat the row's edge pixel
    // (what the original leaves there is not documented).
    public static void ShiftScanlines(Span<int> pixels,int width,int height,Func<int,int> offset){
        if(width<=0)return;var row=new int[width];
        for(int y=0;y<height;y++){
            int dx=offset(y);if(dx==0)continue;var line=pixels.Slice(y*width,width);line.CopyTo(row);
            for(int x=0;x<width;x++)line[x]=row[Math.Clamp(x-dx,0,width-1)];
        }
    }
}
public sealed partial class WorldSession {
    int liquorLevel,liquorEpoch;long liquorAt;bool liquorWatchesSpeed;
    // S206 clears the client's visual state and the server resends S103; Reset also moves the epoch.
    public int LiquorLevel=>liquorEpoch==WorldEpoch?liquorLevel:0;
    public int LiquorPhase=>TimedBuffRules.LiquorPhase(MagicStatusClock()-liquorAt);
    void ReceiveStrup(LoginReader r){
        int seconds=r.H();byte strength=r.C(),weight=r.C(),type=r.C();r.Finish();
        if(Status!=null)Status=Status with{Strength=strength,Weight=weight}; // Full S8 supplies both otherwise.
        ReceiveAbilityUp(TimedBuffRules.PhysicalEnchantStr,TimedBuffRules.DressMighty,"strup",seconds,type);
    }
    void ReceiveDexup(LoginReader r){
        int seconds=r.H();byte dexterity=r.C(),type=r.C();r.Finish();
        if(Status!=null)Status=Status with{Dexterity=dexterity};
        ReceiveAbilityUp(TimedBuffRules.PhysicalEnchantDex,TimedBuffRules.DressDexterity,"dexup",seconds,type);
    }
    // Type is the bonus: dress skills send 2, physical enchant its configured value, then 1 on stop.
    // A physical enchant configured to 2 starts in the dress slot, so a stop falls back to the other slot.
    void ReceiveAbilityUp(int physical,int dress,string source,int seconds,byte type){
        int skill=type==2?dress:physical;
        if(seconds==0&&!magicBuffs.ContainsKey(skill))skill=skill==dress?physical:dress;
        ReceiveBuff(skill,seconds,source);
    }
    void ReceiveLiquor(LoginReader r){
        uint id=r.D();byte type=r.C();r.Finish();
        // Type 8 is the third-speed cake state, sent for any object; it is not drunkenness.
        if(type>3){Unhandled(103);return;}
        if(id!=(Status?.ObjectId??Actor?.ObjectId))return;
        // The server ends drunkenness on haste without any S103.
        if(!liquorWatchesSpeed){liquorWatchesSpeed=true;SpeedChanged+=()=>{if(Actor?.Speed==1)liquorLevel=0;};}
        liquorLevel=type;liquorEpoch=WorldEpoch;liquorAt=MagicStatusClock();
    }
    void ReceiveBlessOfEva(LoginReader r){
        uint id=r.D();int seconds=r.H();r.Finish();
        if((Status?.ObjectId??Actor?.ObjectId) is uint own&&own!=id)return;
        ReceiveBuff(TimedBuffRules.WaterBreath,seconds,"bless-of-eva",name:"水中呼吸");
    }
    void ReceiveDelSkill(LoginReader r,byte[] plain){
        // Same frame as S164: tier flag, 28 or 32 masks, padded to 8. Decode fully before mutating.
        if(plain.Length is not (32 or 40))throw new ProtocolException("truncated skill masks");
        byte tier=r.C();if(tier is not (32 or 50 or 100))throw new ProtocolException("unknown skill tier");
        int groups=plain.Length==40?32:28;var removed=new List<int>();
        for(int row=0;row<groups;row++){byte mask=r.C();for(int bit=0;bit<8;bit++)if((mask&(1<<bit))!=0)removed.Add(row*8+bit+1);}
        r.Finish();RemoveLearnedMagic(removed);
    }
}
public sealed partial class ClientForm {
    bool liquorFramePainting;
    // Program.OnPaint raises Paint from base.OnPaint before it draws, on the same Graphics.
    protected override void CreateHandle(){Paint-=PaintLiquorWave;Paint+=PaintLiquorWave;base.CreateHandle();}
    // A per-row warp needs the finished frame, and this module does not own OnPaint. While drunk the
    // handler runs the unchanged OnPaint into an off-screen frame (the nested Paint returns at once),
    // warps its rows, draws it, then moves the outer pass's remaining drawing out of view.
    void PaintLiquorWave(object? sender,PaintEventArgs e){
        if(liquorFramePainting||!OnlineMode||!login.World.Renderable)return;
        int level=login.World.LiquorLevel;if(level==0)return;
        var viewport=PlayerSceneBounds;if(viewport.Width<=0||viewport.Height<=0)return;
        using var frame=new Bitmap(viewport.Width,viewport.Height,PixelFormat.Format32bppArgb);
        liquorFramePainting=true;
        try{using var g=Graphics.FromImage(frame);g.Clear(BackColor);g.TranslateTransform(-viewport.X,-viewport.Y);OnPaint(new PaintEventArgs(g,viewport));}
        finally{liquorFramePainting=false;}
        // The original warps an 800x480 frame at 1x; scale rows and offsets with the scene zoom.
        int phase=login.World.LiquorPhase;float zoom=Math.Max(Camera.Zoom,.01f);
        WarpLiquorFrame(frame,row=>(int)Math.Round(TimedBuffRules.ScanlineOffset(level,phase,(int)(row/zoom))*zoom));
        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
        e.Graphics.DrawImage(frame,new Rectangle(viewport.Location,frame.Size));
        e.Graphics.TranslateTransform(0,ClientSize.Height+viewport.Height+65536);
    }
    internal static void WarpLiquorFrame(Bitmap frame,Func<int,int> offset){
        var bits=frame.LockBits(new(0,0,frame.Width,frame.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try{unsafe{TimedBuffRules.ShiftScanlines(new Span<int>((void*)bits.Scan0,bits.Stride/4*frame.Height),bits.Stride/4,frame.Height,offset);}}
        finally{frame.UnlockBits(bits);}
    }
}
