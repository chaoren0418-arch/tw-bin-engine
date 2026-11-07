namespace P06;

public enum AfflictionTint : byte { Normal, Poison, Gray }

public sealed partial class WorldSession {
    readonly Dictionary<uint,AfflictionTint> afflictionTints=[];
    int afflictionWorldEpoch;
    public ushort BlindMode {get;private set;}
    public bool AfflictionInputLocked=>paralysis.Any(mode=>mode!=6);

    public WorldSession(){
        // Reset() clears Status before ResetOpacity emits its existing lifecycle event.
        // Map/position changes keep Status and must retain self blindness and poison.
        OpacityObserved+=_=>{if(Status==null){afflictionTints.Clear();BlindMode=0;}};
        CombatSoundStateChanged+=PruneAfflictionTints;
    }

    void PruneAfflictionTints(){
        bool changed=afflictionWorldEpoch!=WorldEpoch;
        foreach(uint id in afflictionTints.Keys.ToArray())
            if(id!=Status?.ObjectId&&(changed||!objects.ContainsKey(id)))afflictionTints.Remove(id);
        afflictionWorldEpoch=WorldEpoch;
    }

    public AfflictionTint TintFor(uint objectId){
        if(afflictionTints.TryGetValue(objectId,out var tint))return tint;
        byte flags=Actor?.ObjectId==objectId?Actor.Flags:objects.TryGetValue(objectId,out var other)?other.Flags:(byte)0;
        return (flags&1)!=0?AfflictionTint.Poison:AfflictionTint.Normal;
    }

    void ReceivePoison(LoginReader r){
        uint id=r.D();byte green=r.C(),gray=r.C();r.Finish();
        if(id==0||green>1||gray>1||green+gray>1)throw new ProtocolException("invalid S165 poison appearance");
        PruneAfflictionTints();
        if(id!=Status?.ObjectId&&!objects.ContainsKey(id))return;
        afflictionTints[id]=green!=0?AfflictionTint.Poison:gray!=0?AfflictionTint.Gray:AfflictionTint.Normal;
    }

    void ReceiveParalysis(LoginReader r){
        // Keep the existing ActionLocked gate and S202/7 recovery-wrapper semantics.
        // All movement/attack sources, including queued sends, already use that gate.
        bool wasLocked=AfflictionInputLocked;
        ReceiveTeleport(202,r);
        if(AfflictionInputLocked&&!wasLocked){
            // Invalidate queued requests even if an unlock arrives before their send gate.
            AppearanceRevision++;
            CancelPickupPresentation("affliction");
        }
    }

    void ReceiveBlind(LoginReader r){
        ushort mode=r.H();r.Finish();
        if(mode>2)throw new ProtocolException("invalid S47 blindness mode");
        BlindMode=mode;
    }
}

public static class AfflictionColors {
    // RGB555 presentation policy; original-client palette parity is not claimed.
    public static ushort Tint(ushort color,AfflictionTint tint){
        int red=(color>>10)&31,green=(color>>5)&31,blue=color&31;
        int light=(red*3+green*6+blue)/10;
        return tint switch {
            AfflictionTint.Poison=>(ushort)((light/3<<10)|(light<<5)|light/3),
            AfflictionTint.Gray=>(ushort)((light<<10)|(light<<5)|light),
            _=>color
        };
    }
    public static ushort Darken(ushort color)=>(ushort)((((color>>10)&31)/4<<10)|(((color>>5)&31)/4<<5)|(color&31)/4);
}

public sealed partial class Renderer {
    Patch ComposeAfflictionPatch(WorldSession session,Rectangle area,List<Draw> commands,SceneOpacity? opacity){
        ushort blind=session.BlindMode;
        if(blind!=0){
            // Cover the complete map background too, including pixels outside actor bounds.
            area=Bounds;
            var mapCommands=ActiveScene.ToHashSet();
            commands=ActiveScene.Concat(commands.Where(c=>!mapCommands.Contains(c)))
                .OrderBy(c=>c,DrawComparer.Instance).ThenBy(c=>c.ObjectId).ToList();
        }
        var tints=commands.Where(c=>c.Actor).Select(c=>c.ObjectId).Distinct()
            .ToDictionary(id=>id,id=>session.TintFor(id==0?session.Actor?.ObjectId??0:id));
        var colors=new ushort[area.Width*area.Height];var mask=new bool[colors.Length];
        var owners=Enumerable.Repeat(-1,colors.Length).ToArray();var entityOwners=new uint?[colors.Length];
        FillShipBackdrop(area,colors,mask);
        foreach(var command in commands){
            var draw=command;
            if(draw.Actor&&tints[draw.ObjectId] is var tint&&tint!=AfflictionTint.Normal)
                draw=draw with{Tile=draw.Tile with{Pixels=draw.Tile.Pixels.Select(p=>p with{Color=AfflictionColors.Tint(p.Color,tint)}).ToArray()}};
            Blit(draw,area,colors,mask,owners,entityOwners,opacity?.Get(command));
        }
        if(blind!=0)for(int i=0;i<colors.Length;i++)
            colors[i]=blind==1?(owners[i]==-2&&entityOwners[i]==null?colors[i]:(ushort)0):AfflictionColors.Darken(colors[i]);
        return new(area,colors,mask,owners,commands,entityOwners);
    }
}
