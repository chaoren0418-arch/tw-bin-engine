using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    LoginConnection? opacityObserver;InventoryCapture? opacityCapture;
    readonly List<object> opacityFrames=[];int recordedOpacityRevision=-1;SceneOpacity? recordedOpacityState;
    void PrepareOpacityCapture(){opacityObserver=new();opacityCapture=new(login,opacityObserver,true);}
    void RecordOpacityFrame(){
        if(opacityCapture==null||login.World.Opacity is not {} state)return;if(state==recordedOpacityState&&state.Revision==recordedOpacityRevision)return;recordedOpacityState=state;recordedOpacityRevision=state.Revision;
        var snap=JsonSerializer.SerializeToElement(state.Snapshot());opacityFrames.Add(new{at=Environment.TickCount64,epoch=login.World.WorldEpoch,appliedSequence=login.AppliedPacketSequence,actor=login.World.Actor,opacity=snap,backgroundUpdate=renderer.LastBackgroundUpdate});
    }
    async Task OpacityDemo(){
        var s=JsonSerializer.Deserialize<OpacityCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_OPACITY_CASE")!))!;using var observer=opacityObserver!;var log=opacityCapture!;using var otherRenderer=new Renderer(World.Assets);var otherWorld=new World(World.Assets);var catalog=new ObjectAssets(World.Assets.Root);
        async Task Shot(string name){
            sceneCapturePending=true;try{
            RefreshScene();PublishSceneWeb(true);var cacheSourceEvidence=renderer.CaptureCacheFrame(objectPatch!,login.World.Opacity,sceneWebCrop,Path.Combine(evidence!,name));int expectedSequence=sceneWebSequence,expectedEpoch=sceneWebEpoch,expectedRevision=sceneWebRevision;string expectedHash=sceneWebHash!;var capturedFrame=JsonSerializer.SerializeToElement(new{at=Environment.TickCount64,cacheSourceEvidence,world=login.World.Snapshot(),opacity=login.World.Opacity?.Snapshot(),objects=ObjectUiSnapshot(),appliedSequence=login.AppliedPacketSequence,sequence=expectedSequence,epoch=expectedEpoch,revision=expectedRevision,sha256=expectedHash,sceneWebCrop,backgroundUpdate=renderer.LastBackgroundUpdate});File.WriteAllBytes(Path.Combine(evidence!,name+"-web-source.png"),sceneWebBytes!);Update();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new(Point.Empty,Size));b.Save(Path.Combine(evidence!,name+"-native.png"));}
            observer.CaptureWorld(()=>{var a=observer.World.Actor!;otherWorld.Teleport(a.X,a.Y);otherWorld.Face(a.Direction);var p=otherRenderer.ComposeObjects(otherWorld,observer.World,catalog,out var visuals,out var missing);var center=World.DisplayPosition;using var b=otherRenderer.SceneView(p,new((int)center.X-360,(int)center.Y-240,720,480));b.Save(Path.Combine(evidence!,name+"-observer-renderer.png"));log.Check("ui-observer-render-"+name,new{kind="actual stationary observer connection, same renderer, no separate native window",visuals,missing,opacity=observer.World.Opacity!.Snapshot()});return true;});
            await web!.ExecuteScriptAsync("document.getElementById('scenePanel').scrollIntoView({block:'start'})");await Task.Delay(80);
            JsonElement d=default;bool ready=false;long deadline=Environment.TickCount64+2000;
            while(Environment.TickCount64<deadline){
                string dom=await web.ExecuteScriptAsync("JSON.stringify({scene:window.renderedScene,loaded:document.getElementById('sceneImage').complete,width:document.getElementById('sceneImage').naturalWidth,rect:(()=>{const r=document.getElementById('sceneImage').getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height,dpr:devicePixelRatio}})(),error:window.sceneDecodeError})");
                d=JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Deserialize<string>(dom)!);
                var decodedScene=d.GetProperty("scene");
                ready=d.GetProperty("loaded").GetBoolean()&&d.I("width")==640&&decodedScene.ValueKind==JsonValueKind.Object&&decodedScene.I("sequence")==expectedSequence&&decodedScene.I("epoch")==expectedEpoch&&decodedScene.I("opacityRevision")==expectedRevision&&decodedScene.GetProperty("sha256").GetString()==expectedHash;
                if(ready)break;await Task.Delay(20);
            }
            if(!ready)throw new Exception("actual Web decoded scene sequence/hash/revision/epoch not ready within2seconds: "+d);
            await using(var f=File.Create(Path.Combine(evidence!,name+"-web.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);
            Json.Save(Path.Combine(evidence!,name+"-scene.json"),new{kind="actual WinForms DrawToBitmap and independent WebView CapturePreview; native WebView placeholder is not web proof",capturedFrame,currentAfterCapture=login.World.Snapshot(),web=d,sourceSceneSequence=sceneWebSequence,sourceSceneHash=sceneWebHash,sceneWebCrop,objects=ObjectUiSnapshot(),backgroundUpdate=renderer.LastBackgroundUpdate});log.Check("ui-shot-"+name,new{web=d,sourceSceneHash=sceneWebHash,backgroundUpdate=renderer.LastBackgroundUpdate});
            }finally{sceneCapturePending=false;}
        }
        async Task Focus(OpacitySafety guard){
            long start=Environment.TickCount64,end=start+2000;
            for(int attempt=1;attempt<=2&&Environment.TickCount64<end;attempt++){
                guard.Check();BringToFront();Activate();bool requested=SetForegroundWindow(Handle);ReturnGameFocus();long until=Math.Min(end,Environment.TickCount64+1000);
                while(Environment.TickCount64<until){guard.Check();if(GameInputAllowed&&GetForegroundWindow()==Handle){log.Check("opacity-own-native-focus",new{attempt,elapsed=Environment.TickCount64-start,GameInputAllowed,active=ActiveForm==this,ownHandle=Handle.ToInt64(),foreground=GetForegroundWindow().ToInt64(),requested});return;}await Task.Delay(20);}
            }
            throw new Exception("opacity actual focus unavailable after2attempts/2seconds");
        }
        int transShots=0;
        async Task Move(int h,string mode,OpacitySafety guard){
            int before=login.World.PredictedSteps;
            if(mode=="native"){
                if(!GameInputAllowed||GetForegroundWindow()!=Handle)await Focus(guard);
                // Isometric keyboard model uses screen deltas; choose the key producing this world heading.
                var keys=new[]{Keys.Home,Keys.Up,Keys.PageUp,Keys.Right,Keys.PageDown,Keys.Down,Keys.End,Keys.Left};
                Keys key=keys[h];OnKeyDown(new KeyEventArgs(key));try{await guard.Wait(()=>login.World.PredictedSteps==before+1,"native OnKeyDown admitted");}finally{OnKeyUp(new KeyEventArgs(key));}
            }else{await web!.ExecuteScriptAsync($"document.getElementById('m{h}').click()");await guard.Wait(()=>login.World.PredictedSteps==before+1,"actual Web move DOM admitted");}
            if(transShots<2&&login.World.Opacity is {} state&&JsonSerializer.SerializeToElement(state.Snapshot()).GetProperty("states").EnumerateArray().Any(v=>(v[2].GetInt32()&0xc00)!=0)){await Task.Delay(60);guard.Check();await Shot("transition-"+(++transShots));}
        }
        async Task Select(uint id,string mode,OpacitySafety guard){
            guard.Check();RefreshScene();if(!HasVisibleObjectPixel(id))throw new Exception("actual observer has no visible pixel");
            if(mode=="native"){await Focus(guard);ClickScene(VisiblePoint(id));}
            else{ClickScene(MagicEmptySelectionPoint());if(login.World.TargetId!=null)throw new Exception("native empty pixel failed clear");lastWebState="";PublishWebState();await Task.Delay(60);log.Check("opacity-before-Web-target-from-null",new{id,target=login.World.TargetId});await web!.ExecuteScriptAsync($"document.getElementById('magicTargets').value='{id}';document.getElementById('magicTargets').dispatchEvent(new Event('change'))");}
            await guard.Wait(()=>login.World.TargetId==id,"native/Web visible selection");log.Check("opacity-visible-selection",new{mode,id,positivePixels=HasVisibleObjectPixel(id)});
        }
        try{
            await Until(()=>webReady,2000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,2000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);charactersBox.SelectedItem=s.Character;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,2000);
            await OpacityScenario.Run(login,observer,s,log,Move,Shot,Select);log.Save(s.Output,"pass");
        }catch(Exception ex){NetworkExitCode=3;log.Save(s.Output,"fail",ex);}finally{Json.Save(Path.Combine(evidence!,"opacity-rendered-frames.json"),new{kind="actual UI composition frame samples, original state at render time, no forced timing",frames=opacityFrames});login.Cancel();observer.Cancel();Close();}
    }
}

