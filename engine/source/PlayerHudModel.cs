namespace P06;

// UI commands retain the displayed session identity. No prediction of server-owned state.
public readonly record struct PlayerHudStamp(int Generation,int WorldEpoch,uint Actor);
public enum PlayerHudKind { Item, Magic }
public sealed record PlayerHudBinding(PlayerHudKind Kind,uint Id);
public sealed record PlayerHudEntry(PlayerHudBinding Binding,string Name,string Detail,ushort? Icon,bool Equipped,bool Supported);
public sealed record PlayerHudSnapshot(PlayerHudStamp Stamp,string Name,string Character,uint Hp,uint MaxHp,uint Mp,uint MaxMp,string State,string Notice,bool Busy,string Target,bool Self,PlayerHudEntry[] Items,PlayerHudEntry[] Skills);
// Original macro box: eight shortcut slots on F5–F12 (docs/reference/ui-layout.md §7 #5).
public static class PlayerHudKeys {
    public const int SlotCount=8,ItemDefaultSlots=4;
    public static int Slot(Keys keyData)=>keyData is >=Keys.F5 and <=Keys.F12?keyData-Keys.F5:-1;
    public static string Label(int slot)=>$"F{5+slot}";
    // Game focus is required; chat, WebView and native inputs keep their own function keys.
    public static bool Allowed(Keys keyData,bool active,bool inputAllowed,bool focused)=>Slot(keyData)>=0&&active&&inputAllowed&&focused;
}
public sealed class PlayerHudSlots {
    sealed class Configuration {public readonly PlayerHudBinding?[] Slots=new PlayerHudBinding?[PlayerHudKeys.SlotCount];public bool ItemsInitialized,SkillsInitialized;}
    // Normal product has one fixed server profile. Original actor object IDs distinguish its characters.
    readonly Dictionary<uint,Configuration> characters=[];
    Configuration current=new();uint actor;
    public IReadOnlyList<PlayerHudBinding?> Bindings=>current.Slots;
    public void Observe(PlayerHudStamp stamp){actor=stamp.Actor;if(actor==0){current=new();return;}if(!characters.TryGetValue(actor,out var config))characters[actor]=config=new();current=config;}
    public void PopulateDefaults(PlayerHudSnapshot state){
        if(actor!=state.Stamp.Actor||actor==0)return;
        const int items=PlayerHudKeys.ItemDefaultSlots,skills=PlayerHudKeys.SlotCount-items;
        if(!current.ItemsInitialized&&state.Items.Length>0){int n=0;foreach(var entry in state.Items.Where(x=>x.Supported&&!x.Equipped).Take(items))current.Slots[n++]=entry.Binding;current.ItemsInitialized=true;}
        if(!current.SkillsInitialized&&state.Skills.Length>0){int n=items;foreach(var entry in state.Skills.Where(x=>x.Supported).Take(skills))current.Slots[n++]=entry.Binding;current.SkillsInitialized=true;}
    }
    public void Assign(int slot,PlayerHudBinding? binding){if(slot<0||slot>=current.Slots.Length)throw new ArgumentOutOfRangeException(nameof(slot));current.Slots[slot]=binding;if(slot<PlayerHudKeys.ItemDefaultSlots)current.ItemsInitialized=true;else current.SkillsInitialized=true;}
    public PlayerHudEntry? Resolve(int slot,PlayerHudSnapshot state){if(actor!=state.Stamp.Actor)return null;var binding=current.Slots[slot];return binding==null?null:(binding.Kind==PlayerHudKind.Item?state.Items:state.Skills).FirstOrDefault(x=>x.Binding==binding);}
}

// This adapter is also exercised without a Form or a network connection by the offline checks.
public static class PlayerHudActions {
    public static PlayerHudStamp Stamp(LoginConnection connection)=>new(connection.SessionGeneration,connection.World.WorldEpoch,connection.World.Actor?.ObjectId??0);
    public static bool Current(LoginConnection connection,PlayerHudStamp stamp)=>connection.State==LoginState.InWorld&&stamp.Actor!=0&&stamp==Stamp(connection);
    // R6-D (WO §4, byte_13CE51E): while a ghost, the macro slots (F5–F12 and slot double-click, legacy HUD and native
    // MacroPanel alike — both reach this adapter) do not use items or skills. Nothing is selected and nothing is sent.
    public static bool GhostBlocks(LoginConnection connection)=>connection.GhostLocalBlocked;
    public static async Task<bool> Activate(LoginConnection connection,PlayerHudStamp stamp,PlayerHudBinding binding,bool walking){
        if(!Current(connection,stamp)||walking||connection.MovementBusy||GhostBlocks(connection))return false;
        if(binding.Kind==PlayerHudKind.Item){
            var item=connection.World.Inventory.FirstOrDefault(x=>x.ObjectId==binding.Id);
            if(item==null||!item.SupportedUse||item.Count==0||!connection.World.SelectItem(item.ObjectId))return false;
            return await connection.UseSelectedItem();
        }
        if(binding.Id>int.MaxValue||!connection.World.LearnedMagic.Any(x=>x.Id==(int)binding.Id&&x.Supported)||!connection.World.SelectMagic((int)binding.Id))return false;
        return await connection.CastSelectedMagic();
    }
}

public static class PlayerHudGeometry {
    public const int BaseHeight=198,DrawerHeight=236;
    public static Rectangle Scene(Size client,int diagnosticWidth,bool playing,bool drawer,int legacyLeft=0){
        int top=playing?0:140;
        int bottom=playing?BaseHeight+(drawer?DrawerHeight:0):80;
        return new(legacyLeft,top,Math.Max(1,client.Width-diagnosticWidth-legacyLeft),Math.Max(1,client.Height-top-bottom));
    }
}
