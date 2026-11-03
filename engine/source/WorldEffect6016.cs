using P24.MapEffectEvents;
namespace P06;
public sealed partial class WorldSession {
    public Effect6016Host? Effects6016 {get;private set;}
    public void Attach6016(Effect6016Host host){Effects6016?.Clear(SceneClock());Effects6016=host;if(MapCache!=null)Sync6016(SceneClock());}
    public static BlockSnapshot Snapshot6016(OriginalMapBlock block)=>new(block.Name,block,
        block.Effects.Select((effect,ordinal)=>new EffectPlacement(ordinal,effect.Id,effect.X,effect.Y)).ToArray());
    OriginalMapCache? effectCache;
    void Evict6016(OriginalMapBlock block)=>Effects6016?.RetireLifetime(block,SceneClock());
    void Sync6016(long now){if(MapCache is {} cache){if(!ReferenceEquals(effectCache,cache)){if(effectCache!=null)effectCache.Evicting-=Evict6016;effectCache=cache;cache.Evicting+=Evict6016;}Effects6016?.Synchronize(cache.MapId,cache.Slots.OfType<OriginalMapBlock>().Select(Snapshot6016).ToArray(),now);}}
    void Clear6016(long now){if(effectCache!=null)effectCache.Evicting-=Evict6016;effectCache=null;Effects6016?.Clear(now);}
    public IReadOnlyDictionary<EffectKey,EffectFrameState> Frames6016()=>Effects6016?.Frames().ToDictionary(s=>s.Owner.Key)??new Dictionary<EffectKey,EffectFrameState>();
    public static Frame MapEffectFrame(MapLocalEffect effect,string block,int ordinal,int map,long elapsed,IReadOnlyDictionary<EffectKey,EffectFrameState> states){
        if(effect.Id!=6016)return effect.Appearance.At(elapsed);
        return effect.Appearance.Frames[states.TryGetValue(new(map,block,ordinal),out var state)?state.Frame:11];
    }
}
