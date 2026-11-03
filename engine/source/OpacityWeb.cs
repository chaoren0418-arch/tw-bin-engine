using System.Text.Json;
using System.Security.Cryptography;
namespace P06;
public sealed partial class ClientForm {
    long lastSceneWeb;int sceneWebSequence;string? sceneWebHash;Rectangle sceneWebCrop;byte[]? sceneWebBytes;int sceneWebEpoch,sceneWebRevision;bool sceneCapturePending;
    void PublishSceneWeb(bool force=false){
        if(sceneCapturePending&&!force)return;
        if(!webReady||web?.CoreWebView2==null||web.IsDisposed)return;
        long now=Environment.TickCount64;if(!force&&now-lastSceneWeb<50)return;lastSceneWeb=now;
        if(!login.World.Renderable||objectPatch==null){web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{v=1,kind="scene",available=false,epoch=login.World.WorldEpoch,sequence=++sceneWebSequence}));return;}
        var center=World.DisplayPosition;sceneWebCrop=Rectangle.Intersect(new((int)center.X-320,(int)center.Y-220,640,400),renderer.Bounds);
        using var bitmap=renderer.SceneView(objectPatch,sceneWebCrop);using(var graphics=Graphics.FromImage(bitmap))PaintObjectPresence(graphics,p=>new(p.X-sceneWebCrop.X,p.Y-sceneWebCrop.Y),new RectangleF(0,0,bitmap.Width,bitmap.Height));using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);byte[] bytes=stream.ToArray();sceneWebBytes=bytes;sceneWebEpoch=login.World.WorldEpoch;sceneWebRevision=login.World.Opacity?.Revision??0;sceneWebHash=Convert.ToHexString(SHA256.HashData(bytes));
        web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{v=1,kind="scene",available=true,epoch=login.World.WorldEpoch,sequence=++sceneWebSequence,png="data:image/png;base64,"+Convert.ToBase64String(bytes),sha256=sceneWebHash,crop=new{sceneWebCrop.X,sceneWebCrop.Y,sceneWebCrop.Width,sceneWebCrop.Height},actor=login.World.Actor,opacityRevision=login.World.Opacity?.Revision??0}));
    }
}

