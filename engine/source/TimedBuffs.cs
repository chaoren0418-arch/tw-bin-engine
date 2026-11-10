using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace P06;
// Server formats and the verified/inferred split: docs/protocol/timed-buffs.md.
public static class TimedBuffRules {
    public const int PhysicalEnchantStr=42,DressMighty=109,PhysicalEnchantDex=26,DressDexterity=110;
    // Item status with no skill id; negative keys are the MagicStatus convention for those.
    public const int WaterBreath=-3126;
    // EF2§3 (T227), UI§4: TW16 S_BREATH (sub_931F60, VA 0x931F60 / FO 0x531360) only calls
    // sub_F05A90(seconds, 11, 0, -1, 1): status effect 11 WATER_BREATH_S in display mode 0, no scene effect.
    // The effect id indexes Tile.pak effectlist2.xml, which the buff-icons module reads at run time.
    public const int WaterBreathEffect=11,WaterBreathDisplayMode=0;
    // Only the S126 mapping is owned here; other buffs have no verified effect id in this module.
    public static int? StatusEffect(int skill)=>skill==WaterBreath?WaterBreathEffect:null;
    // AM§4(c), TW16 sub_F01E50: after lighting, screen row j moves horizontally by
    // (int)(level*3.5*sin((phase+j)*pi/50)). Phase advances every 50 ms through 0..199 (sub_F05FD0),
    // so the 100-row wave travels one period in 5 s and returns home at phase 200.
    public const int LiquorTickMs=50,LiquorPhases=200;
    public static int LiquorPhase(long elapsedMs)=>(int)(Math.Max(0,elapsedMs)/LiquorTickMs%LiquorPhases);
    public static int ScanlineOffset(int level,int phase,int row)=>level is 1 or 2 or 3?(int)(level*3.5*Math.Sin((phase+row)*Math.PI/50)):0;
    // AM§4(c), FU4§2 (T287／#680, R3-H 補做 T288／#689): sub_81B0E0 (VA 0x81B0E0 / FO 0x41A4E0) is true in 16-bit colour
    // mode (callers sub_97B5F0 / sub_9E8E50: RGB565 path, 1856-byte = 928 px × 2 scanline stride). Only the 32-bit path
    // (false) halves the row offset (C integer division). The engine follows the 16-bit path, the 3.81 litoddraw
    // generation (推測), so the product is always full amplitude. The halving branch is kept for tests only.
    public static Func<bool> LiquorFullWave {get;set;}=()=>true;
    public static int LiquorOffset(int level,int phase,int row,bool fullWave)=>fullWave?ScanlineOffset(level,phase,row):ScanlineOffset(level,phase,row)/2;
    public static int LiquorOffset(int level,int phase,int row)=>LiquorOffset(level,phase,row,LiquorFullWave());
    // AM§4(c): S103 type 0 on map 67 or 420 (byte_13CE5E8, set by sub_F09C70) leaves drunkenness level 1.
    // Type 0 also stops the 50 ms phase timer, so the wave stays frozen at its last phase.
    public static bool MinimumLiquorMap(ushort? map)=>map is 67 or 420;
    public const int MinimumLiquorLevel=1;
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
// Data for the status icon row: an effectlist2.xml id plus the T153 timer. Drawing is not done here.
public sealed record StatusEffectIcon(int Effect,int DisplayMode,int Skill,string Name,int Seconds);
public sealed partial class WorldSession {
    // Live buffs that have a verified effect id; remaining seconds come from the S126 timer in MagicBuffs.
    public StatusEffectIcon[] StatusEffectIcons {
        get {
            long now=MagicStatusClock();
            return MagicBuffs.Where(b=>TimedBuffRules.StatusEffect(b.Skill)!=null)
                .Select(b=>new StatusEffectIcon(TimedBuffRules.StatusEffect(b.Skill)!.Value,TimedBuffRules.WaterBreathDisplayMode,b.Skill,b.Name,b.Remaining(now)))
                .Where(i=>i.Seconds>0).ToArray();
        }
    }
    [WorldSnapshot("statusEffects")] object StatusEffectSnapshot()=>StatusEffectIcons.Select(i=>new{effect=i.Effect,mode=i.DisplayMode,skill=i.Skill,name=i.Name,seconds=i.Seconds}).ToArray();
    int liquorLevel,liquorEpoch;long liquorAt;bool liquorWatchesSpeed;int? liquorFrozenPhase;
    // S206 clears the client's visual state and the server resends S103; Reset also moves the epoch.
    public int LiquorLevel=>liquorEpoch==WorldEpoch?liquorLevel:0;
    public int LiquorPhase=>liquorFrozenPhase??TimedBuffRules.LiquorPhase(MagicStatusClock()-liquorAt);
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
    // R7-B (T341/#804): object +547 (0x223), the third-speed flag that sub_4FAAF0 reads for the emote interval (2265 ms).
    // Only the own value is kept; other objects' +547 (animation timing, sub_A3BC30) stays an unhandled gap.
    // Not tied to WorldEpoch: the server sends type 8 only when the cake is used or at login (CharBuffTable), never after
    // a teleport, so a map change must not drop it. Reset (new session) does.
    uint? ownThirdSpeed;
    public bool OwnThirdSpeed=>ownThirdSpeed is uint id&&id==(Status?.ObjectId??Actor?.ObjectId);
    [WorldReset] void ResetThirdSpeed()=>ownThirdSpeed=null;
    void ReceiveLiquor(LoginReader r){
        uint id=r.D();byte type=r.C();r.Finish();
        uint? own=Status?.ObjectId??Actor?.ObjectId;
        // sub_9343C0 (0x934401–0x934425): type 0 → +547＝0; type 8 → +547＝1 and the type is then handled as 0
        // (own drunkenness ends, or level 1 on maps 67／420). Type 8 is the third-speed cake state, not drunkenness.
        if(type==8){
            if(id!=own){Unhandled(103);return;}
            ownThirdSpeed=id;type=0;
        }
        else if(type==0&&id==own)ownThirdSpeed=null;
        if(type>3){Unhandled(103);return;}
        if(id!=own)return;
        // The server ends drunkenness on haste without any S103.
        if(!liquorWatchesSpeed){liquorWatchesSpeed=true;SpeedChanged+=()=>{if(Actor?.Speed==1){liquorLevel=0;liquorFrozenPhase=null;}};}
        if(type==0&&TimedBuffRules.MinimumLiquorMap(MapId)){
            // Freeze the phase the wave had (0 when it was not running in this world).
            liquorFrozenPhase=LiquorLevel>0?LiquorPhase:0;liquorLevel=TimedBuffRules.MinimumLiquorLevel;liquorEpoch=WorldEpoch;return;
        }
        liquorFrozenPhase=null;liquorLevel=type;liquorEpoch=WorldEpoch;liquorAt=MagicStatusClock();
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
        bool full=TimedBuffRules.LiquorFullWave();
        WarpLiquorFrame(frame,row=>(int)Math.Round(TimedBuffRules.LiquorOffset(level,phase,(int)(row/zoom),full)*zoom));
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
