using P06;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

partial class Checks {
    static void DevTools(Assets assets,string output){
        var keyGate=typeof(ClientForm).GetMethod("IsDevToolsKey",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(bool normal in new[]{false,true})foreach(Keys key in Enumerable.Range((int)Keys.F1,12).Select(k=>(Keys)k)){
            foreach(Keys modifiers in new[]{Keys.None,Keys.Control,Keys.Shift,Keys.Alt,Keys.Control|Keys.Shift|Keys.Alt})
                Check(!(bool)keyGate.Invoke(null,[key|modifiers,normal])!,"no developer action for "+normal+" "+(key|modifiers));
            bool expected=key is >=Keys.F9 and <=Keys.F12||!normal&&key is Keys.F2 or Keys.F3 or Keys.F6 or Keys.F7 or Keys.F8;
            Check((bool)keyGate.Invoke(null,[key|Keys.Control|Keys.Shift,normal])! ==expected,"exact developer chord "+normal+" "+key);
        }

        // Initialize only the base Form, not the game constructor/timers/audio/network.
        var form=(ClientForm)RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));
        typeof(Form).GetConstructor(Type.EmptyTypes)!.Invoke(form,[]);
        using(form)using(var c=Seed())using(var renderer=new Renderer(assets)){
            form.ClientSize=new(1180,800);
            Set(form,"login",c);Set(form,"normalProduct",true);Set(form,"renderer",renderer);
            Set(form,"<Input>k__BackingField",new InputState());Set(form,"<World>k__BackingField",new World(assets));
            Set(form,"<Camera>k__BackingField",new Camera());Set(form,"events",new List<object>());
            Set(form,"playerHudHeld",new HashSet<Keys>());Set(form,"clock",Stopwatch.StartNew());
            Set(form,"stageRoot",Path.GetDirectoryName(output)!);Set(form,"evidence",Path.Combine(Path.GetDirectoryName(output)!,"dev-focus",Environment.ProcessId.ToString()));
            Set(form,"webHost",new Panel());Set(form,"bar",new Panel());Set(form,"normalPanels",new TabControl());
            Call(form,"InitDevTools");var tools=Get<DevToolsForm>(form,"devTools");
            // Hidden independent test host, no product startup or connection.
            tools.Opacity=0;tools.ShowInTaskbar=false;
            Check(tools.Owner==form&&form.Controls.Count==0,"all developer controls outside the game parent");
            foreach(bool online in new[]{false,true}){
                Set(c,"State",online?LoginState.InWorld:LoginState.Offline);
                var before=form.PlayerSceneBounds;var point=new Point(20,20);
                form.Input.Down(Keys.Up);Get<HashSet<Keys>>(form,"playerHudHeld").Add(Keys.F5);
                Call(form,"ShowDevTools",Get<Panel>(form,"webHost"));
                Check(form.Input.Held.Count==0&&Get<HashSet<Keys>>(form,"playerHudHeld").Count==0&&!Get<bool>(form,"gameFocus"),"tool focus clears held movement/macro keys "+online);
                tools.Size=new(920,700);Get<Panel>(form,"webHost").Width=700;
                Check(form.PlayerSceneBounds==before&&form.PlayerSceneAccepts(point),"tool visibility/resize cannot change scene or consume game coordinates "+online);
                Call(tools,"OnFormClosing",new FormClosingEventArgs(CloseReason.UserClosing,false));
                Check(!tools.Visible&&!tools.IsDisposed&&Get<bool>(form,"gameFocus")&&form.PlayerSceneBounds==before,"X hides reusable tools and restores game focus "+online);
            }
            Set(c,"State",LoginState.Offline);
            var oldPosition=form.World.Snapshot();
            foreach(Keys key in Enumerable.Range((int)Keys.F1,12).Select(k=>(Keys)k))form.HandleDown(key);
            Check(System.Text.Json.JsonSerializer.Serialize(oldPosition)==System.Text.Json.JsonSerializer.Serialize(form.World.Snapshot())&&form.Input.Held.Count==0,"normal offline bare F1-F12 cannot teleport/connect/capture/open tools");
            form.Camera.View=new(1180,800);form.Camera.ViewOffset=new(12,34);
            using var logical=(Bitmap)Call(form,"CaptureLogicalScene")!;
            Check(logical.Size==new Size(800,600)&&form.Camera.View==new Size(1180,800)&&form.Camera.ViewOffset==new PointF(12,34),"logical capture is 800x600 and restores live camera");
            logical.Save(Path.Combine(Path.GetDirectoryName(output)!,"logical-regression.png"));
            tools.Bounds=new(80,90,920,700);tools.SaveBounds();
            using var restored=new DevToolsForm(Path.Combine(Path.GetDirectoryName(output)!,"runtime","dev-tools-window.json"));
            Check(restored.Bounds==tools.Bounds,"developer window bounds persist");
            tools.Dispose();form.CompleteWebJournal();
        }
    }
}
