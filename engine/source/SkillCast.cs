namespace P06;

public enum SkillTargetKind { None, Self, Object, Location, Name, Bookmark, Summon }
// These are request arguments, not skill templates. Coordinates and names are never
// reinterpreted as object IDs; omitted special arguments must not become zero bytes.
public sealed record SkillCastTarget(SkillTargetKind Kind, uint ObjectId=0,
    ushort? X=null, ushort? Y=null, string? Name=null, string Text="",
    ushort? MapId=null, uint? BookmarkId=null, uint? SummonId=null);

public static class SkillCast {
    public static bool Supported(int skill)=>skill is >=1 and <=220;
    // C_UseSkill.start / ClientPacketStructure.validateSkill, see docs/protocol/skills.md.
    // This is a protocol layout switch, not copied skills-table data.
    public static string Wire(int skill) {
        if(!Supported(skill))throw new ArgumentOutOfRangeException(nameof(skill));
        return skill switch {
            116 or 118=>"S", 113=>"DHHS", 5 or 69=>"HD", 58 or 63=>"HH",
            4 or 6 or 7 or 10 or 15 or 16 or 17 or 18 or 22 or 25 or 28 or 30 or 34 or 38
                or 45 or 46 or 50 or 65 or 74 or 77 or 108 or 132 or 184 or 187 or 192 or 194
                or 202 or 203 or 207 or 208 or 213=>"DHH",
            _=>"D"
        };
    }
    public static bool AllowsDeadTarget(int skill)=>skill is 41 or 61 or 75;
    public static byte[] Request(int skill,SkillCastTarget target,uint caster=0) {
        string wire=Wire(skill);
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write((byte)6);writer.Write((byte)((skill-1)>>3));writer.Write((byte)((skill-1)&7));
        void Require(bool valid){if(!valid)throw new ArgumentException("技能目標參數不完整或類型不符",nameof(target));}
        void Position(){Require(target.X.HasValue&&target.Y.HasValue);writer.Write(target.X!.Value);writer.Write(target.Y!.Value);}
        void Text(string? value,bool required){
            Require(value!=null&&(!required||!string.IsNullOrWhiteSpace(value))&&!value.Contains('\0'));
            byte[] bytes=LoginPackets.TextEncoding.GetBytes(value!);
            Require(bytes.Length<=255);writer.Write(bytes);writer.Write((byte)0);
        }
        uint Object(){Require(target.Kind==SkillTargetKind.Object&&target.ObjectId!=0);return target.ObjectId;}
        switch(wire) {
            case "S":Require(target.Kind==SkillTargetKind.Name);Text(target.Name,true);break;
            case "HD":Require(target.Kind==SkillTargetKind.Bookmark&&target.MapId.HasValue&&target.BookmarkId.HasValue);writer.Write(target.MapId!.Value);writer.Write(target.BookmarkId!.Value);break;
            case "HH":Require(target.Kind==SkillTargetKind.Location);Position();break;
            case "DHH":case "DHHS":writer.Write(Object());Position();if(wire=="DHHS")Text(target.Text,false);break;
            default:
                switch(target.Kind) {
                    case SkillTargetKind.None:writer.Write(0u);break;
                    case SkillTargetKind.Self:Require(caster!=0);writer.Write(caster);break;
                    case SkillTargetKind.Object:writer.Write(Object());break;
                    case SkillTargetKind.Summon:Require(skill==51&&target.SummonId.HasValue);writer.Write(target.SummonId!.Value);break;
                    default:Require(false);break;
                }
                break;
        }
        return LoginPackets.Pad(stream.ToArray());
    }
}

public sealed partial class LoginConnection {
    sealed record PreparedSkillCast(byte[] Bytes,uint Target,ushort X,ushort Y,WorldObject? Object);

    public bool CanCastSelectedMagic(SkillCastTarget? target=null)=>TryPrepareSkillCast(target,out _);
    bool TryPrepareSkillCast(SkillCastTarget? requested,out PreparedSkillCast? prepared) {
        prepared=null;
        if(!MovementAvailable||World.SelectedMagicId is not int id||!SkillCast.Supported(id)
            ||!World.LearnedMagic.Any(m=>m.Id==id)||World.Actor is not {} actor)return false;
        var selected=World.Target;
        var target=requested;
        if(target==null) {
            target=SkillCast.Wire(id) switch {
                "S"=>new(SkillTargetKind.Name,Name:selected?.Name),
                "HD"=>null, // A bookmark must be chosen explicitly, including random teleport (ID 0).
                "HH"=>selected==null?null:new(SkillTargetKind.Location,X:selected.X,Y:selected.Y),
                "DHH" or "DHHS"=>selected==null?null:new(SkillTargetKind.Object,selected.ObjectId),
                _ when id==51=>new(SkillTargetKind.Summon,SummonId:0),
                _ when World.MagicTargetsOwn(id)=>new(SkillTargetKind.Self),
                _=>selected==null?null:new(SkillTargetKind.Object,selected.ObjectId)
            };
        }
        if(target==null)return false;
        uint visualTarget=actor.ObjectId;ushort x=actor.X,y=actor.Y;WorldObject? obj=null;
        if(target.Kind==SkillTargetKind.Object) {
            if(target.ObjectId==actor.ObjectId) { target=target with{X=actor.X,Y=actor.Y}; }
            else {
                obj=World.Objects.FirstOrDefault(o=>o.ObjectId==target.ObjectId);
                if(obj==null||obj.AppearancePending||obj.Dead&&!SkillCast.AllowsDeadTarget(id))return false;
                x=obj.X;y=obj.Y;target=target with{X=x,Y=y};
            }
            visualTarget=target.ObjectId;
        } else if(target.Kind==SkillTargetKind.Location) {
            if(target.X is not ushort tx||target.Y is not ushort ty)return false;
            x=tx;y=ty;visualTarget=0;
        }
        try { prepared=new(SkillCast.Request(id,target,actor.ObjectId),visualTarget,x,y,obj);return true; }
        catch(ArgumentException) { return false; }
    }
}
