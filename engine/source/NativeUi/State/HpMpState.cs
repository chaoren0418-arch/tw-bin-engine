using P06.NativeUi;
namespace P06.NativeUi {
    // ui-hpmp (Issue #465). Settings and state adapters for the HP/MP floating window and the over-head bars.
    // Source of every value: docs/reference/ui-layout.md §2 "HP／MP 浮動窗與頭上條（V1，Issue #456）".

    /// <summary>
    /// ClientConfig #10 FloatingHPMP and #11 showCharacterHPMP (V1: descriptor at VA 0x11570D8). Defaults are the descriptor
    /// defaults, which also equal the .data initial values. Kept in memory for the session; the original protobuf save is not
    /// implemented here (see the issue-465 report).
    /// </summary>
    public sealed record HpMpSettings(
        bool ShowCharacterHpMp=true,
        bool Show=true,
        int Status=2,
        int Shape=1,
        bool Thickness=false,
        bool ShowRecovery=true,
        bool ShowPercent=true,
        int PositionX=-1,
        int PositionY=-1) {
        public static readonly HpMpSettings Default=new();
        /// <summary>HpMpWindow ctor sub_ECD2A0 (0xECD375–0xECD3BC): (−1, −1) → (135, 465).</summary>
        public static readonly Point DefaultPosition=new(135,465);
        /// <summary>Drag cap (sub_ED8D40) and load modulus (sub_59DD30): y ≤ 550.</summary>
        public const int MaxY=550;
        /// <summary>Re-layout cap (sub_ED8F20): x + frame width ≤ 799.</summary>
        public const int MaxRight=799;
        /// <summary>status: 0 short, 1 middle, 2 long. shape: 1 horizontal (HP | MP), 0 vertical (HP over MP).</summary>
        public HpMpSettings Normalized()=>this with{Status=Math.Clamp(Status,0,2),Shape=Shape==0?0:1};
        /// <summary>Load rule: (−1, −1) → (135, 465); otherwise x % 800, y % 550.</summary>
        public Point InitialPosition(){
            if(PositionX==-1&&PositionY==-1)return DefaultPosition;
            return new(Math.Max(0,PositionX%UiRoot.LogicalWidth),Math.Max(0,PositionY%MaxY));
        }
    }

    /// <summary>
    /// One object that may carry an over-head bar. Box is the sprite bbox in logical (view) pixels. HpPercent/MpPercent are the
    /// server-sent percentages for other objects (null = not sent → not drawn). For Self they are null: the panel computes
    /// them from the server's own HP/MP (sub_8E7290).
    /// </summary>
    /// <remarks>Origin (FU8§1.1, T356/#820): self only — P = sub_F1A750(obj+16), the sprite block origin, in the same view
    /// pixels as Box (product: Camera.ToView(Coordinates.Screen(foot) − (24, 12))). null → the bbox rule is used.</remarks>
    public sealed record HpMpHeadTarget(uint ObjectId,bool Self,Rectangle Box,int? HpPercent=null,int? MpPercent=null,Point? Origin=null);

    /// <summary>Extra state the HP/MP module reads; absent → no over-head bars (floating window still uses IUiGameState).</summary>
    public interface IUiHpMpState {
        IReadOnlyList<HpMpHeadTarget> HeadTargets { get; }
        /// <summary>Own class code byte_1542AA1 (FU6§2; LoginCharacter.Class); picks the own blink checker (7 = warrior). null = unknown.</summary>
        byte? OwnClass=>null;
    }

    /// <summary>IUiGameState plus the over-head targets. Every IUiGameState member forwards to Base unchanged.</summary>
    public sealed class UiHpMpGameState(IUiGameState inner,IReadOnlyList<HpMpHeadTarget> headTargets,byte? ownClass=null):IUiGameState,IUiHpMpState {
        public IUiGameState Base=>inner;
        public IReadOnlyList<HpMpHeadTarget> HeadTargets=>headTargets;
        public byte? OwnClass=>ownClass;
        public bool InWorld=>inner.InWorld;
        public string? CharacterName=>inner.CharacterName;
        public int? Level=>inner.Level;
        public long? Hp=>inner.Hp;
        public long? MaxHp=>inner.MaxHp;
        public long? Mp=>inner.Mp;
        public long? MaxMp=>inner.MaxMp;
        public long Now=>inner.Now;
    }
}

namespace P06 {
    public sealed partial class ClientForm {
        /// <summary>
        /// Adds the over-head targets to the native UI state. Same object set as the name display (visible, rendered objects
        /// plus self); boxes go through the world camera, so they are in canvas pixels while the world fills WorldRect.
        /// Others: only objects whose server HP byte is a percentage (0–100) and that are not dead — V1 event 18 adds objects
        /// with +217 ≥ 0. No MP percentage exists for other objects in the model, so their MP bar is never drawn.
        /// </summary>
        IUiGameState WithHpMpState(IUiGameState state){
            if(!state.InWorld||!login.World.Renderable)return new UiHpMpGameState(state,[]);
            var objects=login.World.Objects.ToDictionary(o=>o.ObjectId);
            var list=new List<HpMpHeadTarget>();
            foreach(var t in NameTargets()){
                var a=Camera.ToView(new PointF(t.Bounds.Left,t.Bounds.Top));var b=Camera.ToView(new PointF(t.Bounds.Right,t.Bounds.Bottom));
                var box=Rectangle.FromLTRB((int)Math.Floor(a.X),(int)Math.Floor(a.Y),(int)Math.Floor(b.X),(int)Math.Floor(b.Y));
                if(t.Self){list.Add(new(t.ObjectId,true,box,Origin:SelfHeadOrigin()));continue;}
                if(!objects.TryGetValue(t.ObjectId,out var o)||o.Dead||o.HpPercent is not int hp)continue;
                list.Add(new(t.ObjectId,false,box,hp,null));
            }
            return new UiHpMpGameState(state,list,login.World.OwnClass);
        }
        /// <summary>FU8§1.1: P = Coordinates.Screen(own foot) − (24, 12) (ObjectRendering.cs sub_F1A750 note), through the camera.
        /// null when the own visual is not built yet (the panel then keeps the bbox rule).</summary>
        Point? SelfHeadOrigin(){
            if(renderer.OwnCombatVisual is not {} own)return null;
            var p=Camera.ToView(new PointF(own.Foot.X-24,own.Foot.Y-12));
            return new Point((int)Math.Floor(p.X),(int)Math.Floor(p.Y));
        }
    }
}
