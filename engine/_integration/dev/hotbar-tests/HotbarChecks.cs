using P06;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

// T176 (#450): original macro box has eight shortcut slots on F5–F12; F5 no longer toggles the minimap.
// Memory-only HUD objects; no window is shown, no ClientForm constructor, no assets or network.
partial class Checks {
    static PlayerHudSnapshot HotbarState(PlayerHudStamp stamp,int items=5,int skills=5,bool busy=false)=>new(stamp,"offline","test",100,100,100,100,"","",busy,"",false,
        Enumerable.Range(0,items).Select(i=>new PlayerHudEntry(new(PlayerHudKind.Item,(uint)(900+i)),"item"+i,"×1",null,false,true)).ToArray(),
        Enumerable.Range(0,skills).Select(i=>new PlayerHudEntry(new(PlayerHudKind.Magic,(uint)(1+i)),"spell"+i,"MP 3",null,false,true)).ToArray());
    static void HotbarChecks(){
        // Key map: F5..F12 -> slot 0..7, nothing else.
        Check(PlayerHudKeys.SlotCount==8&&CharacterConfig.SlotCount==8,"eight macro slots in HUD and server config");
        for(int i=0;i<8;i++)Check(PlayerHudKeys.Slot(Keys.F5+i)==i&&PlayerHudKeys.Label(i)=="F"+(5+i),"F"+(5+i)+" maps to slot "+i);
        foreach(var key in new[]{Keys.F1,Keys.F2,Keys.F3,Keys.F4,Keys.F13,Keys.M,Keys.D5,Keys.F5|Keys.Control,Keys.F12|Keys.Shift,Keys.F9|Keys.Alt})
            Check(PlayerHudKeys.Slot(key)==-1,"not a macro key: "+key);
        Check(PlayerHudKeys.Allowed(Keys.F5,true,true,true)&&PlayerHudKeys.Allowed(Keys.F12,true,true,true),"F5 and F12 admitted with game focus in world");
        Check(!PlayerHudKeys.Allowed(Keys.F5,false,true,true)&&!PlayerHudKeys.Allowed(Keys.F9,true,false,true)&&!PlayerHudKeys.Allowed(Keys.F10,true,true,false)&&!PlayerHudKeys.Allowed(Keys.F11|Keys.Control,true,true,true),
            "macro keys excluded at login, with chat/WebView input, unfocused form, or modifiers (F9–F11 tools keep working there)");

        // Model: eight slots, defaults 4 items (F5–F8) then 4 skills (F9–F12).
        var stamp=new PlayerHudStamp(1,1,123);var state=HotbarState(stamp);var slots=new PlayerHudSlots();slots.Observe(stamp);slots.PopulateDefaults(state);
        Check(slots.Bindings.Count==8,"PlayerHudSlots exposes eight bindings");
        Check(Enumerable.Range(0,4).All(i=>slots.Bindings[i]==new PlayerHudBinding(PlayerHudKind.Item,(uint)(900+i))),"defaults: first four supported items on F5–F8");
        Check(Enumerable.Range(4,4).All(i=>slots.Bindings[i]==new PlayerHudBinding(PlayerHudKind.Magic,(uint)(i-3))),"defaults: first four supported skills on F9–F12");
        slots.Assign(7,null);slots.PopulateDefaults(state);Check(slots.Bindings[7]==null,"explicitly cleared F12 stays clear");
        slots.Assign(7,new(PlayerHudKind.Item,904));Check(slots.Resolve(7,state)?.Name=="item4","F12 resolves an assigned item");
        try{slots.Assign(8,null);throw new Exception("slot 8 accepted");}catch(ArgumentOutOfRangeException){Check(true,"ninth slot rejected");}

        // Native HUD view: eight tiles labelled F5..F12, each one activates its own binding.
        using(var view=new PlayerHudView{Size=new(1000,434)}){
            var shortcuts=(FlowLayoutPanel)typeof(PlayerHudView).GetField("shortcuts",F)!.GetValue(view)!;
            var activated=new List<PlayerHudBinding>();view.ActivateEntry+=(_,binding)=>activated.Add(binding);
            view.UpdateState(state,_=>null);
            var tiles=shortcuts.Controls.OfType<PlayerHudTile>().ToArray();
            Check(shortcuts.AccessibleName=="快捷列 F5 至 F12"&&tiles.Length==8,"HUD mounts eight shortcut tiles");
            Check(tiles.Select((t,i)=>t.AccessibleName!.StartsWith("F"+(5+i)+"  ")).All(x=>x),"tiles labelled F5..F12 in order");
            Check(tiles.All(t=>t.Width>=64&&t.Width*8<=shortcuts.Width),"eight tiles fit the shortcut row");
            for(int i=0;i<8;i++)view.ActivateSlot(i);
            Check(activated.Count==8&&activated[0].Id==900&&activated[3].Id==903&&activated[4]==new PlayerHudBinding(PlayerHudKind.Magic,1)&&activated[7]==new PlayerHudBinding(PlayerHudKind.Magic,4),"each slot activates its own default binding");
            view.UpdateState(state with{Busy=true},_=>null);view.ActivateSlot(7);Check(activated.Count==8,"busy blocks F12 slot");
            var menu=view.Controls.OfType<FlowLayoutPanel>().Single(c=>c.AccessibleName=="玩家功能");
            Check(menu.Controls.OfType<Button>().All(b=>!b.Text.Contains("F5")&&!b.Text.Contains("F9")),"menu buttons no longer advertise F5/F9");

            // ClientForm routing without constructing the form.
            using var c=Seed();var formState=HotbarState(PlayerHudActions.Stamp(c));view.UpdateState(formState,_=>null);activated.Clear();
            var form=(ClientForm)RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));GC.SuppressFinalize(form);
            var held=new HashSet<Keys>();
            Set(form,"login",c);Set(form,"normalProduct",true);Set(form,"playerHud",view);Set(form,"playerHudHeld",held);
            var tryKey=typeof(ClientForm).GetMethod("TryPlayerHudKey",F)!;bool Try(Keys key)=>(bool)tryKey.Invoke(form,[key])!;
            Check(Try(Keys.F12)&&activated.Count==1&&activated[0]==new PlayerHudBinding(PlayerHudKind.Magic,4),"F12 in world activates the eighth slot");
            Check(Try(Keys.F12)&&activated.Count==1,"held F12 does not repeat");
            held.Remove(Keys.F12);Check(Try(Keys.F12)&&activated.Count==2,"released F12 activates again");
            Check(Try(Keys.F9)&&activated.Count==3&&activated[2]==new PlayerHudBinding(PlayerHudKind.Magic,1),"F9 in world is the fifth macro slot");
            Check(Try(Keys.F5)&&activated.Count==4&&activated[3].Id==900,"F5 in world is the first macro slot, not the minimap");
            Check(Try(Keys.F1)&&activated.Count==4,"old F1–F4 shortcuts are inert in world");
            Check(!Try(Keys.M),"other keys continue to world input");
            c.Cancel();held.Clear();Check(!Try(Keys.F6)&&activated.Count==4,"outside world F6 stays a network/tool key");
        }

        // Minimap: no keyboard toggle; one menu button opens it.
        Check(typeof(ClientForm).GetMethod("ProcessMinimapKeyboard",F)==null&&typeof(ClientForm).GetMethod("MinimapKeyAllowed",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)==null,"minimap F5 keyboard handler removed");
        Check(typeof(ClientForm).GetMethod("ProcessPlayerHudKeyboard",F)!=null,"ProcessCmdKey macro hook present");
    }
}
