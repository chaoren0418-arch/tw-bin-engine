using System.Collections.Concurrent;
using System.Text.RegularExpressions;
namespace P06;
// Display-only server packets. See docs/protocol/display-notices.md for formats and evidence.
// Nothing here is an acknowledgement: LastServerMessage stays untouched so S245/S105 cannot cancel a pending clan or trade flow.
public sealed record IdentifyDescNotice(ushort DescId,ushort MessageId,string[] Arguments,string? Weight,string Text,bool Recognized,long At);
public sealed record RedNotice(ushort Id,string[] Arguments,string Text,bool Recognized,long At);
public sealed record TrueTargetMark(uint TargetId,uint ObjectId,string Message,ushort Gfx);
public static class DisplayNoticeText {
    public const int RedNoticeMs=5000,IdentifyNoticeMs=8000;
    // S60 seconds value 127 never schedules the clear timer (sub_A452C0, docs/reference/actions-and-names.md 2.5).
    public const byte PermanentPinkSeconds=127;
    public static readonly Color PinkName=NameColors.ToColor(NameColors.Pink);
    static readonly Regex Control=new(@"\\f.",RegexOptions.Singleline|RegexOptions.CultureInvariant),Argument=new(@"%(\d)",RegexOptions.CultureInvariant);
    // Templates come from the server's comments on these IDs, without the leading \f1 color code.
    static string? Template(ushort id,int count)=>(id,count) switch{
        (134,3)=>"%0：小型怪物打擊%1 大型怪物打擊%2",
        (135,2)=>"%0：防御力%1 防御具",
        (136,3)=>"%0：飽食度%1［重量%2］",
        (137,3)=>"%0：可用次數%1［重量%2］",
        (138,2)=>"%0：［重量%1］",
        (551,2)=>"你的PK次數已達%0。達%1次將下地獄。",
        (552,2)=>"因為你已經殺了 %0 人所以被打入地獄。 你將在這裡停留 %1 分鐘。",
        (637,1)=>"你必須在此地停留 %0 秒。",
        _=>null};
    // One pass: argument text is literal and never parsed as another format control.
    public static (string Text,bool Recognized) Format(ushort id,string[] arguments)=>Template(id,arguments.Length) is {} template
        ?(Argument.Replace(template,m=>arguments[m.Groups[1].Value[0]-'0']),true)
        :(ServerMessageText.Decode(id,arguments).Text,false);
    // Color codes are presentation: S_ChangeName(pc,true) and the GM form must not change object identity text.
    public static string PlainName(string raw)=>Control.Replace(raw,"");
}
// Name colour (object +204, RGB555). Constants and formula: docs/reference/actions-and-names.md 2.4.
public static class NameColors {
    public const ushort Outline=0x0000,White=0x7FFF,Red=0x7C00,Pink=0x767D;
    public const ushort RedNameGfx=2715;
    // sub_A406C0: L>=500 bluer with justice, 0..499 white, L<0 redder with evil.
    public static ushort Lawful(int lawful){
        if(lawful>=500){int v=Math.Min(31,((lawful-500)>>8)+1);return Rgb(31-v,31-v,31);}
        if(lawful>=0)return White;
        int e=Math.Min(31,((-lawful)>>6)+1);return Rgb(31,31-e,31-e);
    }
    public static ushort Rgb(int r,int g,int b)=>(ushort)((r&31)<<10|(g&31)<<5|(b&31));
    public static Color ToColor(ushort c)=>Color.FromArgb(((c>>10)&31)*255/31,((c>>5)&31)*255/31,(c&31)*255/31);
}
public sealed partial class WorldSession {
    readonly ConcurrentDictionary<uint,(long Until,int Epoch)> pinkNames=new();
    // Pink overrides lawful colour; gfx 2715 is red outside war mode; otherwise S87/S8/S34 lawful decides.
    public ushort NameColor555(uint id,long? now=null){
        bool self=Actor?.ObjectId==id;WorldObject? o=null;if(!self)objects.TryGetValue(id,out o);
        int gfx=self?Actor!.Gfx:o?.Gfx??0;
        if(gfx==NameColors.RedNameGfx&&!ClanWarMode)return NameColors.Red;
        if(IsPinkName(id,now))return NameColors.Pink;
        return NameColors.Lawful(self?Status?.Lawful??Actor!.Lawful:o?.Lawful??0);
    }
    void ReceiveLawful(LoginReader r){
        // S_Lawful(pc): D objId, H lawful. The unused S_Lawful(objid) form appends two more H(-32768).
        uint id=r.D();short lawful=unchecked((short)r.H());
        if(r.Remaining>=4){var extra=(r.H(),r.H());if(extra!=(0x8000,0x8000))throw new ProtocolException("lawful trailer");}
        r.Finish();
        if(Actor?.ObjectId==id){Actor=Actor with{Lawful=lawful};if(Status is {} s&&s.ObjectId==id)Status=s with{Lawful=lawful};ObjectRevision++;}
        else UpdateObject(id,o=>o with{Lawful=lawful});
    }
    readonly ConcurrentDictionary<uint,(TrueTargetMark Mark,int Epoch)> trueTargets=new();
    volatile RedNotice? redNotice;volatile IdentifyDescNotice? identifyDesc;
    public IdentifyDescNotice? LastIdentifyDesc=>identifyDesc;
    public RedNotice? ActiveRedNotice(long? now=null)=>Entered&&redNotice is {} n&&(now??Environment.TickCount64)-n.At<DisplayNoticeText.RedNoticeMs?n:null;
    public IdentifyDescNotice? ActiveIdentifyDesc(long? now=null)=>Entered&&identifyDesc is {} n&&(now??Environment.TickCount64)-n.At<DisplayNoticeText.IdentifyNoticeMs?n:null;
    public bool IsPinkName(uint id,long? now=null)=>pinkNames.TryGetValue(id,out var p)&&p.Epoch==WorldEpoch&&(now??Environment.TickCount64)<p.Until;
    public Color NameColor(uint id,Color normal,long? now=null)=>IsPinkName(id,now)?DisplayNoticeText.PinkName:normal;
    // The server has no end packet and skill 113 has buffDuration 0, so a mark lasts while its target remains in this world.
    public TrueTargetMark? TrueTarget(uint id)=>trueTargets.TryGetValue(id,out var t)&&t.Epoch==WorldEpoch&&InDisplayWorld(id)?t.Mark:null;
    bool InDisplayWorld(uint id)=>objects.ContainsKey(id)||Actor?.ObjectId==id;
    static (ushort Desc,ushort Message,string[] Arguments) ReadIdentifyDesc(byte[] plain,int extra){
        var r=new LoginReader(plain);r.C();ushort desc=r.H(),message=r.H();var values=new string[r.C()+extra];for(int i=0;i<values.Length;i++)values[i]=r.S();r.Finish();return(desc,message,values);
    }
    void ReceiveIdentifyDesc(byte[] plain){
        string? weight=null;(ushort Desc,ushort Message,string[] Arguments) v;
        try{v=ReadIdentifyDesc(plain,0);}
        catch(ProtocolException){
            // S_IdentifyDesc() writes weight after its three counted weapon strings; no other form has an uncounted string.
            v=ReadIdentifyDesc(plain,1);if(v.Message!=134||v.Arguments.Length!=4)throw;
            weight=v.Arguments[3];v.Arguments=v.Arguments[..3];
        }
        // Items outside weapon/armor/etc write only the description id; the rest is padding.
        if(v.Message==0&&v.Arguments.Length==0)return;
        var(text,recognized)=DisplayNoticeText.Format(v.Message,v.Arguments);
        identifyDesc=new(v.Desc,v.Message,v.Arguments,weight,weight==null?text:$"{text}［重量{weight}］",recognized,Environment.TickCount64);
    }
    void ReceiveRedMessage(LoginReader r){
        ushort id=r.H();var values=new string[r.C()];for(int i=0;i<values.Length;i++)values[i]=r.S();r.Finish();
        var(text,recognized)=DisplayNoticeText.Format(id,values);redNotice=new(id,values,text,recognized,Environment.TickCount64);
    }
    void ReceiveChangeName(LoginReader r){
        uint id=r.D();string name=DisplayNoticeText.PlainName(r.S());r.Finish();
        // Actor.Name is the selected-character identity that S87 is checked against; a GM prefix must not replace it.
        if(id==0||name.Length==0||id==Actor?.ObjectId)return;
        UpdateObject(id,o=>o with{Name=name});
    }
    void ReceivePinkName(LoginReader r){
        uint id=r.D();byte seconds=r.C();r.Finish();long now=Environment.TickCount64;
        foreach(var old in pinkNames)if(old.Value.Epoch!=WorldEpoch||now>=old.Value.Until)pinkNames.TryRemove(old.Key,out _);
        // 0 fires the clear immediately; 127 has no timer and stays pink until another S60 or a world change.
        if(seconds==0)pinkNames.TryRemove(id,out _);else pinkNames[id]=(seconds==DisplayNoticeText.PermanentPinkSeconds?long.MaxValue:now+seconds*1000L,WorldEpoch);
        ObjectRevision++;
    }
    void ReceiveTrueTarget(LoginReader r){
        uint target=r.D(),objectId=r.D();string message=r.S();
        // S_TrueTarget(target,gfx) repeats the target id, writes a null string and appends the gfx word.
        ushort gfx=target==objectId&&message.Length==0?r.H():(ushort)0;r.Finish();
        foreach(var old in trueTargets)if(old.Value.Epoch!=WorldEpoch||!InDisplayWorld(old.Key))trueTargets.TryRemove(old.Key,out _);
        if(target!=0)trueTargets[target]=(new(target,objectId,message,gfx),WorldEpoch);
        ObjectRevision++;
    }
}
public sealed partial class ClientForm {
    void PaintDisplayNotices(Graphics g){
        var w=login.World;var viewport=new RectangleF(Camera.ViewOffset,Camera.View);
        foreach(var v in renderer.OwnCombatVisual is {} own?objectVisuals.Append(own):objectVisuals){
            // Pink names recolour the name label itself (NameColor555); no separate pink caption.
            var mark=w.TrueTarget(v.ObjectId);if(mark==null)continue;
            var head=Camera.ToView(new PointF(v.Foot.X,v.Bounds.IsEmpty?v.Foot.Y-64:v.Bounds.Top));
            if(mark!=null){g.FillPolygon(Brushes.OrangeRed,new PointF[]{new(head.X-7,head.Y-34),new(head.X+7,head.Y-34),new(head.X,head.Y-22)});if(mark.Message.Length>0)PaintNoticeText(g,mark.Message,Color.OrangeRed,new PointF(head.X,head.Y-52),viewport);}
        }
        if(ServerStatusMessages.CenterNotice(w) is {} red)PaintNoticeText(g,ResolveNpcText(red),Color.Red,new PointF(viewport.Left+viewport.Width/2,viewport.Top+viewport.Height/3),viewport);
        if(w.ActiveIdentifyDesc() is {} identify)PaintNoticeText(g,ResolveNpcText(identify.Text),Color.White,new PointF(viewport.Left+viewport.Width/2,viewport.Bottom-48),viewport);
    }
    void PaintNoticeText(Graphics g,string text,Color color,PointF center,RectangleF viewport){
        var size=g.MeasureString(text,Font,(int)Math.Max(1,viewport.Width-16));
        var box=new RectangleF(Math.Clamp(center.X-size.Width/2,viewport.Left+4,Math.Max(viewport.Left+4,viewport.Right-size.Width-4)),center.Y,size.Width,size.Height);
        using var background=new SolidBrush(Color.FromArgb(170,0,0,0));using var brush=new SolidBrush(color);g.FillRectangle(background,box);g.DrawString(text,Font,brush,box);
    }
}
